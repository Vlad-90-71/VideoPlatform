using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;
using FileService.Models;

namespace FileService.Services;

public class ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly Lazy<IMinioClient> _clientLazy;
    private IMinioClient Client => _clientLazy.Value;

    private readonly ILogger<ObjectStorageService> _logger;

    public ObjectStorageService(
        IOptions<StorageSettings> settings,
        ILogger<ObjectStorageService> logger)
    {
        _logger = logger;

        // ✅ Используем фабрику
        _clientLazy = MinioClientFactory.CreateLazy(
            settings.Value,
            MinioClientType.Internal,  // Внутренний endpoint
            logger);
    }

    public async Task<Stream> GetObjectAsync(string objectName, string bucketName)
    {
        var memoryStream = new MemoryStream();

        await Client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            }));

        memoryStream.Position = 0;

        _logger.LogInformation("Downloaded object {Bucket}/{Object}", bucketName, objectName);
        return memoryStream;
    }

    public async Task UploadObjectAsync(
        string objectName,
        Stream stream,
        string bucketName,
        string contentType)
    {
        await Client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType));

        _logger.LogInformation("Uploaded object {Bucket}/{Object}", bucketName, objectName);
    }

    public async Task DeleteObjectAsync(string objectName, string bucketName)
    {
        await Client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName));

        _logger.LogInformation("Deleted object {Bucket}/{Object}", bucketName, objectName);
    }

    public async Task DeleteObjectsAsync(IEnumerable<string> objectNames, string bucketName)
    {
        var objects = objectNames.ToList();

        var deleteTasks = objects.Select(async objectName =>
        {
            await DeleteObjectAsync(objectName, bucketName);
        });

        await Task.WhenAll(deleteTasks);

        _logger.LogInformation("Deleted {Count} objects from bucket {Bucket}", objects.Count, bucketName);
    }

    public async Task<List<ObjectItem>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        bool recursive = true)
    {
        var objects = new List<ObjectItem>();
        var tcs = new TaskCompletionSource<bool>();

        var args = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix(prefix ?? string.Empty)
            .WithRecursive(recursive);

        var observable = Client.ListObjectsAsync(args);

        var subscription = observable.Subscribe(
            item =>
            {
                objects.Add(new ObjectItem
                {
                    Key = item.Key ?? string.Empty,
                    Size = (long)item.Size,
                    LastModified = item.LastModifiedDateTime ?? DateTime.MinValue,
                    ETag = item.ETag ?? string.Empty,
                    ContentType = string.Empty
                });
            },
            error =>
            {
                _logger.LogError(error, "Error listing objects in bucket {Bucket} with prefix {Prefix}",
                    bucketName, prefix);
                tcs.TrySetException(error);
            },
            () =>
            {
                tcs.TrySetResult(true);
            });

        await tcs.Task;
        subscription.Dispose();

        _logger.LogInformation("Listed {Count} objects in bucket {Bucket}", objects.Count, bucketName);
        return objects;
    }

    public async Task<bool> ObjectExistsAsync(string objectName, string bucketName)
    {
        try
        {
            await Client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName));

            return true;
        }
        catch (Exception)
        {
            return false;
        }
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