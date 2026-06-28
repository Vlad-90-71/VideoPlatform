using Shared.DTO;
using Shared.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WebApp.Services;

public class FileServiceClient(HttpClient httpClient, ILogger<FileServiceClient> logger) : IFileServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<FileServiceClient> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ✅ Новый метод — инициализация загрузки
    public async Task<Models.InitUploadResponse> InitUploadAsync(Models.InitUploadRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/video/upload/init", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Models.InitUploadResponse>(json, _jsonOptions)!;
    }

    // DTO (должны совпадать с FileService)
    public record InitUploadRequest(string FileName, long FileSize, int ChunkSize, int TotalChunks);
    public record InitUploadResponse(Guid VideoId, int TotalChunks, int ChunkSize, List<ChunkUploadUrlDto> UploadUrls);
    public record ChunkUploadUrlDto(int ChunkIndex, string UploadUrl);

    public async Task<UploadProgressDto> UploadChunkAsync(Guid videoId, string fileName, int chunkIndex, int totalChunks, Stream chunkStream)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(chunkStream), "File", fileName },
            { new StringContent(videoId.ToString()), "VideoId" },
            { new StringContent(fileName), "FileName" },
            { new StringContent(chunkIndex.ToString()), "ChunkIndex" },
            { new StringContent(totalChunks.ToString()), "TotalChunks" }
        };

        var response = await _httpClient.PostAsync("/api/video/upload/chunk", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UploadProgressDto>(json, _jsonOptions)!;  // ← Используем кэшированный
    }

    public async Task<VideoMetadataDto> CompleteUploadAsync(Guid videoId, string fileName, int totalChunks)
    {
        var request = new
        {
            VideoId = videoId,
            FileName = fileName,
            TotalChunks = totalChunks
        };

        var response = await _httpClient.PostAsJsonAsync("/api/video/upload/complete", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VideoMetadataDto>(json, _jsonOptions)!;  // ← Используем кэшированный
    }
    /*
    public async Task<VideoMetadataDto> GetVideoMetadataAsync(Guid videoId)
    {
        var response = await _httpClient.GetAsync($"/api/video/{videoId}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VideoMetadataDto>(json, _jsonOptions)!;  // ← Используем кэшированный
    }
    */
    public async Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId)
    {
        var response = await _httpClient.GetAsync($"/api/video/{videoId}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VideoInfoDto>();
    }

    public async Task<List<VideoInfoDto>> GetAllVideosAsync()
    {
        var response = await _httpClient.GetAsync("/api/video/list");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<VideoInfoDto>>()
            ?? [];
    }
}