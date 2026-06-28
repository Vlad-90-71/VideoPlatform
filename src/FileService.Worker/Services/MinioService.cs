using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;
using Microsoft.Extensions.Options;

namespace FileService.Worker.Services;

public class MinioService(
    IMinioClient minioClient,
    IOptions<StorageSettings> settings,  // ✅ ИЗМЕНЕНО: StorageSettings вместо MinioSettings
    ILogger<MinioService> logger) : IMinioService
{
    private readonly IMinioClient _minioClient = minioClient;
    private readonly StorageSettings _settings = settings.Value;  // ✅ ИЗМЕНЕНО
    private readonly ILogger<MinioService> _logger = logger;

    public async Task<Stream> GetObjectAsync(string objectName, bool fromHls = false)  // ✅ Добавлен параметр
    {
        var bucketName = fromHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;  // ✅ ИЗМЕНЕНО

        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)  // ✅ Теперь не пустой
            .WithObject(objectName)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            }));

        memoryStream.Position = 0;
        _logger.LogInformation("Downloaded object {ObjectName} from MinIO bucket {Bucket}", objectName, bucketName);
        return memoryStream;
    }

    public async Task UploadObjectAsync(string objectName, Stream stream, string contentType, bool isHls = false)
    {
        var bucketName = isHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;  // ✅ ИЗМЕНЕНО

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType));

        _logger.LogInformation("Uploaded object {ObjectName} to MinIO bucket {Bucket}", objectName, bucketName);
    }

    public async Task DeleteObjectAsync(string objectName, bool fromHls = false)
    {
        var bucketName = fromHls ? _settings.VideoHlsBucket : _settings.VideoStorageBucket;  // ✅ ИЗМЕНЕНО

        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName));

        _logger.LogInformation("Deleted object {ObjectName} from MinIO bucket {Bucket}", objectName, bucketName);
    }
}