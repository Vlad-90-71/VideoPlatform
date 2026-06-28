using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;

namespace FileService.Services;

public class MinioService : IMinioService
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

    public async Task<string> UploadChunkAsync(Guid videoId, int chunkIndex, Stream chunkStream, string fileName)
    {
        var objectName = $"{videoId}/chunks/chunk_{chunkIndex:D6}";

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_settings.VideoStorageBucket)
            .WithObject(objectName)
            .WithStreamData(chunkStream)
            .WithObjectSize(chunkStream.Length)
            .WithContentType("application/octet-stream"));

        _logger.LogInformation("Uploaded chunk {ChunkIndex} for video {VideoId}", chunkIndex, videoId);

        return objectName;  // ✅ Возвращаем имя объекта
    }

    public async Task<Stream> GetObjectAsync(string objectName, bool fromHls = false)  // ✅ Добавлен параметр
    {
        var bucketName = fromHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;
        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            }));

        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task UploadObjectAsync(string objectName, Stream stream, string contentType, bool isHls = false)  // ✅ Добавлен параметр
    {
        var bucketName = isHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType));

        _logger.LogInformation("Uploaded {ObjectName} to bucket {Bucket}", objectName, bucketName);
    }

    public async Task DeleteObjectAsync(string objectName, bool fromHls = false)  // ✅ Добавлен параметр
    {
        var bucketName = fromHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;

        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName));

        _logger.LogInformation("Deleted {ObjectName} from bucket {Bucket}", objectName, bucketName);
    }

    public async Task<List<ObjectItem>> ListObjectsAsync(string prefix, bool isHls = false)
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

    public void Dispose()
    {
        (_publicMinioClient as IDisposable)?.Dispose();
    }
}