using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;

namespace FileService.Services;

public class PresignedUrlService(
    IOptions<StorageSettings> settings,
    ILogger<PresignedUrlService> logger) : IPresignedUrlService, IDisposable
{
    private readonly Lazy<IMinioClient> _clientLazy = MinioClientFactory.CreateLazy(
            settings.Value,
            MinioClientType.Public,  // Публичный endpoint
            logger);

    private IMinioClient Client => _clientLazy.Value;

    private readonly ILogger<PresignedUrlService> _logger = logger;

    public async Task<string> GetPresignedUploadUrlAsync(
        string objectName,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        var url = await Client.PresignedPutObjectAsync(args);

        _logger.LogDebug("Generated presigned upload URL for {Bucket}/{Object}", bucketName, objectName);
        return url;
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string objectName,
        string bucketName,
        int expirySeconds = 3600)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        var url = await Client.PresignedGetObjectAsync(args);

        _logger.LogDebug("Generated presigned download URL for {Bucket}/{Object}", bucketName, objectName);
        return url;
    }

    public async Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600)
    {
        var urls = new Dictionary<string, string>();

        foreach (var objectName in objectNames)
        {
            var url = await GetPresignedUploadUrlAsync(objectName, bucketName, contentType, expirySeconds);
            urls[objectName] = url;
        }

        _logger.LogInformation("Generated {Count} presigned upload URLs for bucket {Bucket}",
            urls.Count, bucketName);

        return urls;
    }

    public async Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        int expirySeconds = 3600)
    {
        var urls = new Dictionary<string, string>();

        foreach (var objectName in objectNames)
        {
            var url = await GetPresignedDownloadUrlAsync(objectName, bucketName, expirySeconds);
            urls[objectName] = url;
        }

        _logger.LogInformation("Generated {Count} presigned download URLs for bucket {Bucket}",
            urls.Count, bucketName);

        return urls;
    }

    public void Dispose()
    {
        if (_clientLazy.IsValueCreated)
        {
            (_clientLazy.Value as IDisposable)?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}