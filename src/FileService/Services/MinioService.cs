using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;
using Shared.Models;

namespace FileService.Services;

public class MinioService : IMinioService, IDisposable
{
    private readonly IMinioClient _minioClient;
    private readonly IMinioClient _publicMinioClient;
    private readonly StorageSettings _settings;
    private readonly ILogger<MinioService> _logger;

    public MinioService(IMinioClient minioClient, IOptions<StorageSettings> settings, ILogger<MinioService> logger) 
    {
        _minioClient = minioClient;
        _settings = settings.Value;
        _logger = logger;

        _publicMinioClient = new MinioClient()
            .WithEndpoint(_settings.PublicEndpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithSSL(_settings.UseSSL)
            .WithHttpClient(new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }))
            .Build();
    }

    public async Task<string> GetPresignedUploadUrlAsync(string objectName, string contentType)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(_settings.VideoStorageBucket)  // ✅ Исправлено
            .WithObject(objectName)
            .WithExpiry(_settings.PresignedUrlExpirySeconds);

        var url = await _publicMinioClient.PresignedPutObjectAsync(args);

        _logger.LogDebug("Generated presigned URL for {ObjectName}", objectName);
        return url;
    }

    public async Task<string> GetPresignedDownloadUrlAsync(string objectName, bool isHls = false)
    {
        var bucketName = isHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;

        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(_settings.PresignedUrlExpirySeconds);

        var url = await _publicMinioClient.PresignedGetObjectAsync(args);
        _logger.LogDebug("Generated presigned GET URL for {ObjectName} from bucket {Bucket}", objectName, bucketName);
        return url;
    }

    public async Task<string> GetPresignedDeleteUrlAsync(string objectName, bool isHls = false)
    {
        var bucketName = isHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;

        var args = new PresignedDeleteObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(_settings.PresignedUrlExpirySeconds);

        var url = await _publicMinioClient.PresignedDeleteObjectAsync(args);
        _logger.LogDebug("Generated presigned DELETE URL for {ObjectName} from bucket {Bucket}", objectName, bucketName);
        return url;
    }

    public async Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId)
    {
        try
        {
            // Проверяем наличие HLS плейлиста в MinIO
            var hlsPlaylistPath = $"{videoId}/hls/playlist.m3u8";

            // Получаем список объектов в папке видео
            var objects = await ListObjectsAsync($"{videoId}/", isHls: true);

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
            var storageObjects = await ListObjectsAsync($"{videoId}/", isHls: false);
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
            var hlsObjects = await ListObjectsAsync("", isHls: true);

            // ✅ ВТОРОЙ вызов (опционально) — получаем все объекты из video-storage
            var storageObjects = await ListObjectsAsync("", isHls: false);

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

    private async Task<List<ObjectItem>> ListObjectsAsync(string prefix, bool isHls = false)
    {
        var objects = new List<ObjectItem>();
        var tcs = new TaskCompletionSource<bool>();

        var bucketName = isHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;  // ✅ Исправлено

        var args = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix(prefix)
            .WithRecursive(true);

        var observable = _minioClient.ListObjectsAsync(args);

        var subscription = observable.Subscribe(
            item =>
            {
                objects.Add(new ObjectItem
                {
                    Key = item.Key ?? string.Empty,
                    Size = (long)item.Size,
                    LastModified = item.LastModifiedDateTime ?? DateTime.MinValue
                });
            },
            error =>
            {
                _logger.LogError(error, "Error listing objects with prefix {Prefix}", prefix);
                tcs.TrySetException(error);
            },
            () =>
            {
                tcs.TrySetResult(true);
            });

        await tcs.Task;
        subscription.Dispose();

        return objects;
    }

    public void Dispose()
    {
        (_publicMinioClient as IDisposable)?.Dispose();
    }
}