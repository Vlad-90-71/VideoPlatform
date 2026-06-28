using FileService.Models;
using Microsoft.Extensions.Options;
using Shared.Configuration;
using Shared.DTO;
using Shared.Messaging;
using Shared.Models;

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

    public async Task<string> GetPresignedUploadUrlAsync(string objectName)
    {
        return await _minioService.GetPresignedUploadUrlAsync(objectName, "application/octet-stream");
    }

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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Completing upload for video {VideoId}", request.VideoId);

        if (!_videoMetadata.TryGetValue(request.VideoId, out var metadata))
        {
            metadata = new VideoMetadataDto
            {
                VideoId = request.VideoId,
                OriginalFileName = request.FileName,
                TotalChunks = request.TotalChunks,
                UploadedChunks = request.TotalChunks,
                Status = VideoStatus.Uploading,
                CreatedAt = DateTime.UtcNow
            };
            _videoMetadata[request.VideoId] = metadata;
        }

        var objectName = $"{request.VideoId}/{request.FileName}";

        metadata.Status = VideoStatus.Processing;
        metadata.ProcessedAt = DateTime.UtcNow;

        stopwatch.Restart();
        await _rabbitMqService.PublishProcessVideoCommandAsync(new ProcessVideoCommand
        {
            VideoId = request.VideoId,
            BucketName = _minioSettings.BucketName,
            ObjectName = objectName,
            OriginalFileName = request.FileName,
            TotalChunks = request.TotalChunks,
            CreatedAt = DateTime.UtcNow
        });
        _logger.LogInformation("Published command in {Elapsed}ms", stopwatch.ElapsedMilliseconds);

        _logger.LogInformation("Upload completed for video {VideoId}, command sent to Worker", request.VideoId);
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

    public async Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId)
    {
        try
        {
            // Проверяем наличие HLS плейлиста в MinIO
            var hlsPlaylistPath = $"{videoId}/hls/playlist.m3u8";

            // Получаем список объектов в папке видео
            var objects = await _minioService.ListObjectsAsync($"{videoId}/", isHls: true);

            if (objects.Count == 0)
            {
                return null;
            }

            // Проверяем, есть ли HLS файлы
            var hasHls = objects.Any(o => o.Key.Contains("/hls/"));

            if (!hasHls)
            {
                return new VideoInfoDto
                {
                    VideoId = videoId,
                    FileName = objects.First().Key.Split('/').Last(),
                    Status = "Processing",
                    CreatedAt = DateTime.UtcNow
                };
            }

            // Получаем имя файла из оригинального видео (если оно ещё есть)
            var storageObjects = await _minioService.ListObjectsAsync($"{videoId}/", isHls: false);
            var originalFileName = storageObjects
                .FirstOrDefault(o => !o.Key.Contains("/chunks/") && !o.Key.Contains("/hls/"))
                ?.Key.Split('/').Last() ?? "video.mp4";

            return new VideoInfoDto
            {
                VideoId = videoId,
                FileName = originalFileName,
                HlsPlaylistUrl = hlsPlaylistPath,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video info for {VideoId}", videoId);
            return null;
        }
    }

    public async Task<List<VideoInfoDto>> GetAllVideosAsync()
    {
        try
        {
            // ✅ ОДИН вызов — получаем все объекты из video-hls
            var hlsObjects = await _minioService.ListObjectsAsync("", isHls: true);

            // ✅ ВТОРОЙ вызов (опционально) — получаем все объекты из video-storage
            var storageObjects = await _minioService.ListObjectsAsync("", isHls: false);

            _logger.LogInformation("Found {Count} objects in video-hls, {Count} in video-storage",
                hlsObjects.Count, storageObjects.Count);

            // Группируем по videoId
            var videoGroups = hlsObjects
                .Where(o => o.Key.Contains("/hls/playlist.m3u8"))
                .Select(o => o.Key.Split('/').First())
                .Distinct()
                .ToList();

            var videos = new List<VideoInfoDto>();

            foreach (var videoIdStr in videoGroups)
            {
                if (Guid.TryParse(videoIdStr, out var videoId))
                {
                    var playlistObject = hlsObjects.FirstOrDefault(o => o.Key == $"{videoId}/hls/playlist.m3u8");

                    if (playlistObject != null)
                    {
                        // Ищем оригинальное имя файла в video-storage
                        var originalFile = storageObjects
                            .FirstOrDefault(o => o.Key.StartsWith($"{videoId}/") &&
                                            !o.Key.Contains("/chunks/") &&
                                            !o.Key.Contains("/hls/"));

                        var fileName = originalFile != null
                            ? Uri.UnescapeDataString(originalFile.Key.Split('/').Last())
                            : $"Video_{videoId.ToString()[..8]}";

                        videos.Add(new VideoInfoDto
                        {
                            VideoId = videoId,
                            FileName = fileName,
                            HlsPlaylistUrl = $"{videoId}/hls/playlist.m3u8",
                            Status = "Completed",
                            CreatedAt = playlistObject.LastModified
                        });
                    }
                }
            }

            _logger.LogInformation("Found {Count} videos", videos.Count);
            return [.. videos.OrderByDescending(v => v.CreatedAt)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all videos");
            return [];
        }
    }
}