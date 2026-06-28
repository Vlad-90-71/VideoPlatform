using Shared.Messaging;

namespace FileService.Worker.Services;

public class VideoProcessingService(
    IMinioService minioService,
    IFFmpegService ffmpegService,
    IRabbitMqService rabbitMqService,
    ILogger<VideoProcessingService> logger) : IVideoProcessingService
{
    private readonly IMinioService _minioService = minioService;
    private readonly IFFmpegService _ffmpegService = ffmpegService;
    private readonly IRabbitMqService _rabbitMqService = rabbitMqService;
    private readonly ILogger<VideoProcessingService> _logger = logger;

    // ✅ Лимит параллельных операций с MinIO
    private const int MaxParallelOperations = 10;

    public async Task ProcessVideoAsync(ProcessVideoCommand command)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), command.VideoId.ToString());

        try
        {
            // Отправляем начальное событие
            await _rabbitMqService.PublishVideoProgressAsync(new VideoProgressEvent
            {
                VideoId = command.VideoId,
                ProgressPercentage = 0,
                Status = VideoProcessingStatus.Started,
                Timestamp = DateTime.UtcNow
            });

            // ✅ ОБЪЕДИНЯЕМ чанки из MinIO в локальный файл
            _logger.LogInformation("Merging chunks for video {VideoId}", command.VideoId);
            Directory.CreateDirectory(tempDir);
            var inputPath = Path.Combine(tempDir, "input.mp4");

            await MergeChunksAsync(command.VideoId, command.TotalChunks, inputPath);

            // Конвертируем в HLS
            _logger.LogInformation("Converting video {VideoId} to HLS", command.VideoId);
            var outputDir = Path.Combine(tempDir, "hls");

            var progress = new Progress<int>(async percentage =>
            {
                await _rabbitMqService.PublishVideoProgressAsync(new VideoProgressEvent
                {
                    VideoId = command.VideoId,
                    ProgressPercentage = percentage,
                    Status = VideoProcessingStatus.Processing,
                    Timestamp = DateTime.UtcNow
                });
            });

            var playlistPath = await _ffmpegService.ConvertToHlsAsync(inputPath, outputDir, progress);

            // ✅ ЗАГРУЖАЕМ HLS файлы в MinIO ПАРАЛЛЕЛЬНО
            _logger.LogInformation("Uploading HLS files for video {VideoId} to MinIO", command.VideoId);
            var hlsBasePath = $"{command.VideoId}/hls";

            var segmentFiles = Directory.GetFiles(outputDir, "*.ts");

            // ✅ Параллельная загрузка плейлиста и всех сегментов
            var uploadSemaphore = new SemaphoreSlim(MaxParallelOperations);

            var uploadTasks = new List<Task>
            {
                // Загрузка плейлиста
                Task.Run(async () =>
                {
                    await uploadSemaphore.WaitAsync();
                    try
                    {
                        using var playlistStream = new FileStream(
                            playlistPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                            bufferSize: 81920, useAsync: true);

                        await _minioService.UploadObjectAsync(
                            $"{hlsBasePath}/playlist.m3u8",
                            playlistStream,
                            "application/vnd.apple.mpegurl",
                            isHls: true);

                        _logger.LogDebug("Uploaded playlist.m3u8");
                    }
                    finally
                    {
                        uploadSemaphore.Release();
                    }
                })
            };

            // Загрузка сегментов
            foreach (var segmentFile in segmentFiles)
            {
                var fileName = Path.GetFileName(segmentFile);
                uploadTasks.Add(Task.Run(async () =>
                {
                    await uploadSemaphore.WaitAsync();
                    try
                    {
                        using var segmentStream = new FileStream(
                            segmentFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                            bufferSize: 81920, useAsync: true);

                        await _minioService.UploadObjectAsync(
                            $"{hlsBasePath}/{fileName}",
                            segmentStream,
                            "video/mp2t",
                            isHls: true);

                        _logger.LogDebug("Uploaded segment {FileName}", fileName);
                    }
                    finally
                    {
                        uploadSemaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(uploadTasks);
            _logger.LogInformation("Uploaded {Count} HLS files for video {VideoId}",
                segmentFiles.Length + 1, command.VideoId);

            // ✅ ПАРАЛЛЕЛЬНОЕ удаление чанков
            await DeleteChunksAsync(command.VideoId, command.TotalChunks);

            // Отправляем финальное событие
            await _rabbitMqService.PublishVideoProgressAsync(new VideoProgressEvent
            {
                VideoId = command.VideoId,
                ProgressPercentage = 100,
                Status = VideoProcessingStatus.Completed,
                HlsPlaylistUrl = $"{hlsBasePath}/playlist.m3u8",
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Successfully processed video {VideoId}", command.VideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video {VideoId}", command.VideoId);

            await _rabbitMqService.PublishVideoProgressAsync(new VideoProgressEvent
            {
                VideoId = command.VideoId,
                ProgressPercentage = 0,
                Status = VideoProcessingStatus.Failed,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });

            throw;
        }
        finally
        {
            // Очищаем временные файлы
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp directory {TempDir}", tempDir);
                }
            }
        }
    }

    // ✅ УЛУЧШЕННЫЙ МЕТОД: параллельная загрузка чанков, последовательная запись
    private async Task MergeChunksAsync(Guid videoId, int totalChunks, string outputPath)
    {
        _logger.LogInformation("Downloading {TotalChunks} chunks in parallel for video {VideoId}",
            totalChunks, videoId);

        // ✅ Параллельно скачиваем все чанки в память
        var downloadSemaphore = new SemaphoreSlim(MaxParallelOperations);
        var chunkBuffers = new MemoryStream[totalChunks];

        var downloadTasks = Enumerable.Range(0, totalChunks).Select(async i =>
        {
            await downloadSemaphore.WaitAsync();
            try
            {
                var chunkObjectName = $"{videoId}/chunks/chunk_{i:D6}";
                var chunkStream = await _minioService.GetObjectAsync(chunkObjectName);

                var buffer = new MemoryStream();
                await chunkStream.CopyToAsync(buffer);
                buffer.Position = 0;
                chunkBuffers[i] = buffer;

                // Освобождаем поток из MinIO
                await chunkStream.DisposeAsync();

                _logger.LogDebug("Downloaded chunk {ChunkIndex}/{TotalChunks}", i + 1, totalChunks);
            }
            finally
            {
                downloadSemaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks);
        _logger.LogInformation("Downloaded all chunks, writing to file");

        // ✅ Последовательно записываем в файл (важен порядок!)
        using var finalStream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        for (int i = 0; i < totalChunks; i++)
        {
            using var chunkBuffer = chunkBuffers[i];
            chunkBuffer.Position = 0;
            await chunkBuffer.CopyToAsync(finalStream);
        }

        _logger.LogInformation("Merged {TotalChunks} chunks for video {VideoId}", totalChunks, videoId);
    }

    // ✅ УЛУЧШЕННЫЙ МЕТОД: параллельное удаление
    private async Task DeleteChunksAsync(Guid videoId, int totalChunks)
    {
        _logger.LogInformation("Deleting {TotalChunks} chunks in parallel for video {VideoId}",
            totalChunks, videoId);

        var deleteSemaphore = new SemaphoreSlim(MaxParallelOperations);

        var deleteTasks = Enumerable.Range(0, totalChunks).Select(async i =>
        {
            await deleteSemaphore.WaitAsync();
            try
            {
                var chunkObjectName = $"{videoId}/chunks/chunk_{i:D6}";
                await _minioService.DeleteObjectAsync(chunkObjectName, fromHls: false);
            }
            finally
            {
                deleteSemaphore.Release();
            }
        });

        await Task.WhenAll(deleteTasks);
        _logger.LogInformation("Deleted {TotalChunks} chunks for video {VideoId}", totalChunks, videoId);
    }
}