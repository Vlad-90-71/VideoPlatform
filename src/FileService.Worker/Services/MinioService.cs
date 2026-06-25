using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;
using Microsoft.Extensions.Options;

namespace FileService.Worker.Services;

public class MinioService(IMinioClient minioClient, IOptions<MinioSettings> settings, ILogger<MinioService> logger) : IMinioService
{
    private readonly IMinioClient _minioClient = minioClient;
    private readonly MinioSettings _settings = settings.Value;
    private readonly ILogger<MinioService> _logger = logger;

    public async Task<Stream> GetObjectAsync(string objectName)
    {
        var memoryStream = new MemoryStream();
        
        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            }));
        
        memoryStream.Position = 0;
        _logger.LogInformation("Downloaded object {ObjectName} from MinIO", objectName);
        return memoryStream;
    }

    public async Task UploadObjectAsync(string objectName, Stream stream, string contentType)
    {
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType));
        
        _logger.LogInformation("Uploaded object {ObjectName} to MinIO", objectName);
    }

    public async Task DeleteObjectAsync(string objectName)
    {
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName));
        
        _logger.LogInformation("Deleted object {ObjectName} from MinIO", objectName);
    }
}
