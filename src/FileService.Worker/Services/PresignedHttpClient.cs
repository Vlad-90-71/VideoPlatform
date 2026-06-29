using System.Net.Http.Headers;

namespace FileService.Worker.Services;

public interface IPresignedHttpClient
{
    Task<Stream> DownloadAsync(string presignedUrl);
    Task UploadAsync(string presignedUrl, Stream stream, string contentType);
}

public class PresignedHttpClient(
    HttpClient httpClient,
    ILogger<PresignedHttpClient> logger) : IPresignedHttpClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<PresignedHttpClient> _logger = logger;

    public async Task<Stream> DownloadAsync(string presignedUrl)
    {
        _logger.LogDebug("Downloading from presigned URL");

        var response = await _httpClient.GetAsync(presignedUrl);
        response.EnsureSuccessStatusCode();

        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        _logger.LogDebug("Downloaded {Size} bytes", memoryStream.Length);
        return memoryStream;
    }

    public async Task UploadAsync(string presignedUrl, Stream stream, string contentType)
    {
        _logger.LogDebug("Uploading to presigned URL with content type {ContentType}", contentType);

        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await _httpClient.PutAsync(presignedUrl, content);
        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Uploaded {Size} bytes", stream.Length);
    }
}