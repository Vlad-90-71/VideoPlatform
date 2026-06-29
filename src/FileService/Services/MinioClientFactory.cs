using Microsoft.Extensions.Options;
using Minio;
using Shared.Configuration;

namespace FileService.Services;

public enum MinioClientType
{
    Internal,    // Для прямых операций (внутренний endpoint)
    Public       // Для presigned URL (публичный endpoint)
}

public static class MinioClientFactory
{
    public static IMinioClient Create(
        StorageSettings settings,
        MinioClientType clientType,
        ILogger logger)
    {
        var endpoint = clientType switch
        {
            MinioClientType.Internal => settings.Endpoint,
            MinioClientType.Public => settings.PublicEndpoint,
            _ => throw new ArgumentException($"Unknown client type: {clientType}")
        };

        logger.LogDebug(
            "Creating MinIO client for {ClientType} endpoint: {Endpoint}",
            clientType,
            endpoint);

        return new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(settings.UseSSL)
            .WithHttpClient(new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }))
            .Build();
    }

    public static Lazy<IMinioClient> CreateLazy(
        StorageSettings settings,
        MinioClientType clientType,
        ILogger logger)
    {
        return new Lazy<IMinioClient>(() => Create(settings, clientType, logger));
    }
}