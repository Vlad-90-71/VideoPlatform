using System.Text.Json;
using Shared.DTO;

namespace WebApp.Services;

public class LessonServiceClient(HttpClient httpClient, ILogger<LessonServiceClient> logger) : ILessonServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<LessonServiceClient> _logger = logger;

    // ✅ Кэшируем JsonSerializerOptions
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IEnumerable<LessonDto>> GetAllLessonsAsync()
    {
        var response = await _httpClient.GetAsync("/api/lessons");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LessonDto>>(json, _jsonOptions)!;
    }

    public async Task<LessonDto?> GetLessonByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"/api/lessons/{id}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LessonDto>(json, _jsonOptions);
    }

    public async Task<LessonDto> CreateLessonAsync(string title, string description, Guid? videoId = null)
    {
        var request = new
        {
            Title = title,
            Description = description,
            VideoId = videoId
        };

        var response = await _httpClient.PostAsJsonAsync("/api/lessons", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LessonDto>(json, _jsonOptions)!;
    }

    public async Task<LessonDto?> UpdateLessonAsync(Guid id, string? title, string? description, Guid? videoId)
    {
        var request = new
        {
            Title = title,
            Description = description,
            VideoId = videoId
        };

        var response = await _httpClient.PutAsJsonAsync($"/api/lessons/{id}", request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LessonDto>(json, _jsonOptions);
    }

    public async Task<bool> DeleteLessonAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"/api/lessons/{id}");
        return response.IsSuccessStatusCode;
    }
}