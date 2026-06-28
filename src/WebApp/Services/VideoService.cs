using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Configuration;
using Shared.Constants;
using Shared.DTO;
using Shared.Messaging;
using Shared.Models;
using System.Text;
using System.Text.Json;

namespace WebApp.Services;

public interface IVideoService
{
    Task<VideoMetadataDto> CompleteUploadAsync(Guid videoId, string fileName, int totalChunks);
    Task<VideoMetadataDto?> GetVideoMetadataAsync(Guid videoId);
}

public class VideoService(
    IVideoProgressCache progressCache,
    IConfiguration configuration,
    ILogger<VideoService> logger) : IVideoService
{
    private readonly IVideoProgressCache _progressCache = progressCache;
    private readonly RabbitMqSettings _rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()!;
    private readonly ILogger<VideoService> _logger = logger;

    public async Task<VideoMetadataDto> CompleteUploadAsync(Guid videoId, string fileName, int totalChunks)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Completing upload for video {VideoId}", videoId);

        var metadata = new VideoMetadataDto
        {
            VideoId = videoId,
            OriginalFileName = fileName,
            TotalChunks = totalChunks,
            UploadedChunks = totalChunks,
            Status = VideoStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        // Сохраняем в кэш
        var progressEvent = new VideoProgressEvent
        {
            VideoId = videoId,
            ProgressPercentage = 0,
            Status = VideoProcessingStatus.Started,
            Timestamp = DateTime.UtcNow
        };
        _progressCache.UpdateProgress(progressEvent);

        // Публикуем команду в RabbitMQ
        await PublishProcessVideoCommandAsync(new ProcessVideoCommand
        {
            VideoId = videoId,
            BucketName = "video-storage",  // ✅ Жёстко задано, т.к. это константа
            ObjectName = $"{videoId}/{fileName}",
            OriginalFileName = fileName,
            TotalChunks = totalChunks,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation("Published command in {Elapsed}ms", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation("Upload completed for video {VideoId}, command sent to Worker", videoId);

        return metadata;
    }

    public Task<VideoMetadataDto?> GetVideoMetadataAsync(Guid videoId)
    {
        var progress = _progressCache.GetProgress(videoId);

        if (progress == null)
        {
            return Task.FromResult<VideoMetadataDto?>(null);
        }

        var metadata = new VideoMetadataDto
        {
            VideoId = videoId,
            OriginalFileName = "video.mp4 - " + videoId.ToString(),
            TotalChunks = 0,  // Не хранится в кэше
            UploadedChunks = 0,
            Status = progress.Status switch
            {
                VideoProcessingStatus.Started => VideoStatus.Uploading,
                VideoProcessingStatus.Processing => VideoStatus.Processing,
                VideoProcessingStatus.Completed => VideoStatus.Completed,
                VideoProcessingStatus.Failed => VideoStatus.Failed,
                _ => VideoStatus.Uploading
            },
            CreatedAt = progress.Timestamp,
            ProcessedAt = progress.Status == VideoProcessingStatus.Completed ? progress.Timestamp : null
        };

        return Task.FromResult<VideoMetadataDto?>(metadata);
    }

    private async Task PublishProcessVideoCommandAsync(ProcessVideoCommand command)
    {
        var factory = new ConnectionFactory()
        {
            HostName = _rabbitMqSettings.Host,
            Port = _rabbitMqSettings.Port,
            UserName = _rabbitMqSettings.Username,
            Password = _rabbitMqSettings.Password,
            VirtualHost = _rabbitMqSettings.VirtualHost
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var json = JsonSerializer.Serialize(command);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: MessagingConstants.ProcessVideoExchange,
            routingKey: MessagingConstants.ProcessVideoRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Published ProcessVideoCommand for video {VideoId}", command.VideoId);
    }
}