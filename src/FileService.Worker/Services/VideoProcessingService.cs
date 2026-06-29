using Shared.Messaging;

namespace FileService.Worker.Services;

public class VideoProcessingService(
    IFileServiceClient fileServiceClient,
    IPresignedHttpClient presignedHttpClient,
    IFFmpegService ffmpegService,
    IRabbitMqService rabbitMqService,
    ILogger<VideoProcessingService> logger) : IVideoProcessingService
{
    private readonly IFileServiceClient _fileServiceClient = fileServiceClient;
    private readonly IPresignedHttpClient _presignedHttpClient = presignedHttpClient;
    private readonly IFFmpegService _ffmpegService = ffmpegService;
    private readonly IRabbitMqService _rabbitMqService = rabbitMqService;
    private readonly ILogger<VideoProcessingService> _logger = logger;

    private const int MaxParallelOperations = 10;
    private const string VideoStorageBucket = "video-storage";
    private const string VideoHlsBucket = "video-hls";

    public async Task ProcessVideoAsync(ProcessVideoCommand command)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), command.VideoId.ToString());

        try
        {
            // ✅ Отправляем начальное событие
            await _rabbitMqService.PublishVideoProgressAsync(new VideoProgressEvent
            {
                VideoId = command.VideoId,
                ProgressPercentage = 0,
                Status = VideoProcessingStatus.Started,
                Timestamp = DateTime.UtcNow
            });

            // ✅ ПОЛУЧАЕМ presigned URLs для скачивания чанков
            _logger.LogInformation("Requesting presigned download URLs for video {VideoId}", command.VideoId);
            var chunkObjectNames = Enumerable.Range(0, command.TotalChunks)
                .Select(i => $"{command.VideoId}/chunks/chunk_{i:D6}")
                .ToList();

            var downloadUrls = await _fileServiceClient.GetPresignedDownloadUrlsAsync(
                chunkObjectNames,
                VideoStorageBucket,
                expirySeconds: 7200);

            // ✅ ОБЪЕДИНЯЕМ чанки в локальный файл
            _logger.LogInformation("Merging chunks for video {VideoId}", command.VideoId);
            Directory.CreateDirectory(tempDir);
            var inputPath = Path.Combine(tempDir, "input.mp4");

            await MergeChunksAsync(downloadUrls, inputPath);

            // ✅ Конвертируем в HLS
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

            // ✅ ПОЛУЧАЕМ presigned URLs для загрузки HLS файлов
            _logger.LogInformation("Requesting presigned upload URLs for HLS files of video {VideoId}", command.VideoId);

            var hlsBasePath = $"{command.VideoId}/hls";
            var segmentFiles = Directory.GetFiles(outputDir, "*.ts");

            // Формируем список всех объектов для загрузки
            var hlsObjectNames = new List<string> { $"{hlsBasePath}/playlist.m3u8" };
            hlsObjectNames.AddRange(segmentFiles.Select(f => $"{hlsBasePath}/{Path.GetFileName(f)}"));

            var uploadUrls = await _fileServiceClient.GetPresignedUploadUrlsAsync(
                hlsObjectNames,
                VideoHlsBucket,
                contentType: "video/mp2t",  // Default, playlist будет переопределён
                expirySeconds: 7200);

            // ✅ ЗАГРУЖАЕМ HLS файлы параллельно
            _logger.LogInformation("Uploading HLS files for video {VideoId}", command.VideoId);
            await UploadHlsFilesAsync(playlistPath, segmentFiles, hlsBasePath, uploadUrls);

            // ✅ УДАЛЯЕМ чанки через FileService
            _logger.LogInformation("Deleting chunks for video {VideoId}", command.VideoId);
            await _fileServiceClient.DeleteObjectsAsync(chunkObjectNames, VideoStorageBucket);

            // ✅ Отправляем финальное событие
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

    // ✅ Скачиваем чанки через presigned URLs и записываем в файл
    private async Task MergeChunksAsync(Dictionary<string, string> downloadUrls, string outputPath)
    {
        _logger.LogInformation("Downloading {Count} chunks in parallel", downloadUrls.Count);

        var downloadSemaphore = new SemaphoreSlim(MaxParallelOperations);
        var chunkBuffers = new MemoryStream[downloadUrls.Count];
        var objectNames = downloadUrls.Keys.ToList();

        var downloadTasks = objectNames.Select(async (objectName, index) =>
        {
            await downloadSemaphore.WaitAsync();
            try
            {
                var presignedUrl = downloadUrls[objectName];
                var chunkStream = await _presignedHttpClient.DownloadAsync(presignedUrl);

                var buffer = new MemoryStream();
                await chunkStream.CopyToAsync(buffer);
                buffer.Position = 0;
                chunkBuffers[index] = buffer;

                await chunkStream.DisposeAsync();

                _logger.LogDebug("Downloaded chunk {ChunkIndex}/{TotalChunks}", index + 1, downloadUrls.Count);
            }
            finally
            {
                downloadSemaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks);
        _logger.LogInformation("Downloaded all chunks, writing to file");

        // Последовательно записываем в файл (важен порядок!)
        using var finalStream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        for (int i = 0; i < chunkBuffers.Length; i++)
        {
            using var chunkBuffer = chunkBuffers[i];
            chunkBuffer.Position = 0;
            await chunkBuffer.CopyToAsync(finalStream);
        }

        _logger.LogInformation("Merged {Count} chunks", downloadUrls.Count);
    }

    // ✅ Загружаем HLS файлы через presigned URLs
    private async Task UploadHlsFilesAsync(
        string playlistPath,
        string[] segmentFiles,
        string hlsBasePath,
        Dictionary<string, string> uploadUrls)
    {
        var uploadSemaphore = new SemaphoreSlim(MaxParallelOperations);

        var uploadTasks = new List<Task>();

        // Загрузка плейлиста
        var playlistObjectName = $"{hlsBasePath}/playlist.m3u8";
        if (uploadUrls.TryGetValue(playlistObjectName, out var playlistUrl))
        {
            uploadTasks.Add(Task.Run(async () =>
            {
                await uploadSemaphore.WaitAsync();
                try
                {
                    using var playlistStream = new FileStream(
                        playlistPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 81920, useAsync: true);

                    await _presignedHttpClient.UploadAsync(
                        playlistUrl,
                        playlistStream,
                        "application/vnd.apple.mpegurl");

                    _logger.LogDebug("Uploaded playlist.m3u8");
                }
                finally
                {
                    uploadSemaphore.Release();
                }
            }));
        }

        // Загрузка сегментов
        foreach (var segmentFile in segmentFiles)
        {
            var fileName = Path.GetFileName(segmentFile);
            var segmentObjectName = $"{hlsBasePath}/{fileName}";

            if (uploadUrls.TryGetValue(segmentObjectName, out var segmentUrl))
            {
                uploadTasks.Add(Task.Run(async () =>
                {
                    await uploadSemaphore.WaitAsync();
                    try
                    {
                        using var segmentStream = new FileStream(
                            segmentFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                            bufferSize: 81920, useAsync: true);

                        await _presignedHttpClient.UploadAsync(
                            segmentUrl,
                            segmentStream,
                            "video/mp2t");

                        _logger.LogDebug("Uploaded segment {FileName}", fileName);
                    }
                    finally
                    {
                        uploadSemaphore.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(uploadTasks);
        _logger.LogInformation("Uploaded {Count} HLS files", uploadTasks.Count);
    }
}