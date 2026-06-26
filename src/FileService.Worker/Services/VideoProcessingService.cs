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

            // Скачиваем видео из MinIO
            _logger.LogInformation("Downloading video {VideoId} from MinIO", command.VideoId);
            var videoStream = await _minioService.GetObjectAsync(command.ObjectName);
            
            var inputPath = Path.Combine(tempDir, "input.mp4");
            Directory.CreateDirectory(tempDir);
            
            using (var fileStream = new FileStream(inputPath, FileMode.Create))
            {
                await videoStream.CopyToAsync(fileStream);
            }

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

            // Загружаем HLS файлы в MinIO
            _logger.LogInformation("Uploading HLS files for video {VideoId} to MinIO", command.VideoId);
            var hlsBasePath = $"{command.VideoId}/hls";

            // Загружаем playlist
            using (var playlistStream = new FileStream(playlistPath, FileMode.Open))
            {
                await _minioService.UploadObjectAsync(
                    $"{hlsBasePath}/playlist.m3u8", 
                    playlistStream, 
                    "application/vnd.apple.mpegurl");
            }

            // Загружаем все .ts сегменты
            var segmentFiles = Directory.GetFiles(outputDir, "*.ts");
            foreach (var segmentFile in segmentFiles)
            {
                var fileName = Path.GetFileName(segmentFile);
                using var segmentStream = new FileStream(segmentFile, FileMode.Open);
                await _minioService.UploadObjectAsync(
                    $"{hlsBasePath}/{fileName}", 
                    segmentStream, 
                    "video/mp2t");
            }

            // Удаляем оригинальное видео
            await _minioService.DeleteObjectAsync(command.ObjectName);

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
}
