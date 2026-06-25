using FileService.Models;
using Shared.Configuration;
using Shared.DTO;
using Shared.Messaging;
using Microsoft.Extensions.Options;

namespace FileService.Services;

public class VideoService(
    IMinioService minioService,
    IRabbitMqService rabbitMqService,
    IOptions<MinioSettings> minioSettings,
    ILogger<VideoService> logger) : IVideoService
{
    private readonly IMinioService _minioService = minioService;
    private readonly IRabbitMqService _rabbitMqService = rabbitMqService;
    private readonly MinioSettings _minioSettings = minioSettings.Value;
    private readonly ILogger<VideoService> _logger = logger;

    // In-memory storage for demo (replace with Redis/DB in production)
    private static readonly Dictionary<Guid, VideoMetadataDto> _videoMetadata = [];

    public async Task<UploadProgressDto> UploadChunkAsync(ChunkUploadRequest request)
    {
        if (!_videoMetadata.TryGetValue(request.VideoId, out VideoMetadataDto? metadata))
        {
            metadata = new VideoMetadataDto
            {
                VideoId = request.VideoId,
                OriginalFileName = request.FileName,
                TotalChunks = request.TotalChunks,
                UploadedChunks = 0,
                Status = VideoStatus.Uploading,
                CreatedAt = DateTime.UtcNow
            };
            _videoMetadata[request.VideoId] = metadata;
        }

        using var stream = request.File.OpenReadStream();
        await _minioService.UploadChunkAsync(request.VideoId, request.ChunkIndex, stream, request.FileName);
        metadata.UploadedChunks++;
        metadata.FileSize += request.File.Length;

        return new UploadProgressDto
        {
            VideoId = request.VideoId,
            TotalChunks = metadata.TotalChunks,
            UploadedChunks = metadata.UploadedChunks
        };
    }

    public async Task<VideoMetadataDto> CompleteUploadAsync(UploadCompleteRequest request)
    {
        var metadata = _videoMetadata[request.VideoId];
        
        var objectName = await _minioService.MergeChunksAsync(request.VideoId, request.TotalChunks, request.FileName);
        
        metadata.Status = VideoStatus.Processing;
        metadata.ProcessedAt = DateTime.UtcNow;

        // Отправляем команду на обработку
        await _rabbitMqService.PublishProcessVideoCommandAsync(new ProcessVideoCommand
        {
            VideoId = request.VideoId,
            BucketName = _minioSettings.BucketName,
            ObjectName = objectName,
            OriginalFileName = request.FileName,
            CreatedAt = DateTime.UtcNow
        });

        return metadata;
    }

    public Task<VideoMetadataDto> GetVideoMetadataAsync(Guid videoId)
    {
        if (_videoMetadata.TryGetValue(videoId, out var metadata))
        {
            return Task.FromResult(metadata);
        }
        
        throw new KeyNotFoundException($"Video {videoId} not found");
    }
}
