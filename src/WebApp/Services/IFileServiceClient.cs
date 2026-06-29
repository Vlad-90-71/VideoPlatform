using System.Net.Http.Json;
using Shared.Models;

namespace WebApp.Services;

public interface IFileServiceClient
{
    // ✅ Получение presigned URLs для загрузки (новый API)
    Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600);

    // ✅ Получение списка объектов (новый API)
    Task<List<ObjectItemDto>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        bool recursive = true);
}

public record PresignedUrlsResponse
{
    public Dictionary<string, string> Urls { get; init; } = new();
    public int ExpirySeconds { get; init; }
    public string Operation { get; init; } = string.Empty;
}

public record ListObjectsResponse
{
    public string BucketName { get; init; } = string.Empty;
    public string? Prefix { get; init; }
    public int Count { get; init; }
    public List<ObjectItemDto> Objects { get; init; } = new();
}

public record ObjectItemDto
{
    public string Key { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string ETag { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}