namespace WebApp.Services;

public class FileServiceClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FileServiceClient> logger) : IFileServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _baseUrl = configuration["FileService:BaseUrl"] ?? "http://fileservice:8080";
    private readonly ILogger<FileServiceClient> _logger = logger;

    public async Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600)
    {
        var url = $"{_baseUrl}/api/video/presigned/upload";

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

    public async Task<List<ObjectItemDto>> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        bool recursive = true)
    {
        var url = $"{_baseUrl}/api/video/objects?bucketName={Uri.EscapeDataString(bucketName)}";

        if (!string.IsNullOrEmpty(prefix))
        {
            url += $"&prefix={Uri.EscapeDataString(prefix)}";
        }

        url += $"&recursive={recursive.ToString().ToLower()}";

        _logger.LogDebug("Requesting objects list from FileService: {Url}", url);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListObjectsResponse>();

        if (result?.Objects == null)
        {
            throw new InvalidOperationException("Failed to deserialize objects list");
        }

        _logger.LogInformation("Received {Count} objects from bucket {Bucket}", result.Objects.Count, bucketName);
        return result.Objects;
    }
}
