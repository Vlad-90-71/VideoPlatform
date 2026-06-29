using Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace FileService.Worker.Services;

public interface IFileServiceClient
{
    // ✅ Получение presigned URLs для скачивания
    Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        int expirySeconds = 3600);

    // ✅ Получение presigned URLs для загрузки
    Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600);

    // ✅ Удаление объектов
    Task DeleteObjectsAsync(IEnumerable<string> objectNames, string bucketName);
}

public class FileServiceClient(
    HttpClient httpClient,
    IOptions<FileServiceSettings> settings,
    ILogger<FileServiceClient> logger) : IFileServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly FileServiceSettings _settings = settings.Value;
    private readonly ILogger<FileServiceClient> _logger = logger;

    public async Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        int expirySeconds = 3600)
    {
        var url = $"{_settings.BaseUrl}/api/video/presigned/download";

        var request = new
        {
            objectNames = objectNames.ToList(),
            bucketName,
            expirySeconds
        };

        _logger.LogDebug("Requesting presigned download URLs from FileService");

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PresignedUrlsResponse>();

        if (result?.Urls == null)
        {
            throw new InvalidOperationException("Failed to deserialize presigned download URLs");
        }

        _logger.LogInformation("Received {Count} presigned download URLs", result.Urls.Count);
        return result.Urls;
    }

    public async Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600)
    {
        var url = $"{_settings.BaseUrl}/api/video/presigned/upload";

        var request = new
        {
            objectNames = objectNames.ToList(),
            bucketName,
            contentType,
            expirySeconds
        };

        _logger.LogDebug("Requesting presigned upload URLs from FileService");

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PresignedUrlsResponse>();

        if (result?.Urls == null)
        {
            throw new InvalidOperationException("Failed to deserialize presigned upload URLs");
        }

        _logger.LogInformation("Received {Count} presigned upload URLs", result.Urls.Count);
        return result.Urls;
    }

    public async Task DeleteObjectsAsync(IEnumerable<string> objectNames, string bucketName)
    {
        var url = $"{_settings.BaseUrl}/api/video/objects";

        var request = new
        {
            objectNames = objectNames.ToList(),
            bucketName
        };

        _logger.LogDebug("Requesting deletion of {Count} objects from FileService", request.objectNames.Count);

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted {Count} objects via FileService", request.objectNames.Count);
    }
}

public record PresignedUrlsResponse
{
    public Dictionary<string, string> Urls { get; init; } = [];
    public int ExpirySeconds { get; init; }
    public string Operation { get; init; } = string.Empty;
}