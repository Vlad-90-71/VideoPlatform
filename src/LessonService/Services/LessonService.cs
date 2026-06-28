using LessonService.Data;
using LessonService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Shared.DTO;
using System.Text.Json;

namespace LessonService.Services;

public class LessonService(
    LessonDbContext context,
    IDistributedCache cache,
    ILogger<LessonService> logger) : ILessonService
{
    private readonly LessonDbContext _context = context;
    private readonly IDistributedCache _cache = cache;
    private readonly ILogger<LessonService> _logger = logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IEnumerable<LessonDto>> GetAllLessonsAsync()
    {
        var cacheKey = "lessons:all";
        
        // Пробуем получить из кэша
        var cachedData = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var lessons = JsonSerializer.Deserialize<List<LessonDto>>(cachedData);
            if (lessons != null)
            {
                _logger.LogInformation("Retrieved all lessons from cache");
                return lessons;
            }
        }

        // Получаем из БД
        var lessonsList = await _context.Lessons
            .OrderBy(l => l.CreatedAt)
            .Select(l => new LessonDto
            {
                Id = l.Id,
                Title = l.Title,
                Description = l.Description,
                VideoId = l.VideoId ?? Guid.Empty,
                VideoUrl = l.VideoUrl,
                VideoStatus = l.VideoStatus,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            })
            .ToListAsync();

        // Сохраняем в кэш
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheDuration
        };
        
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(lessonsList), options);
        
        _logger.LogInformation("Retrieved {Count} lessons from database", lessonsList.Count);
        return lessonsList;
    }

    public async Task<LessonDto?> GetLessonByIdAsync(Guid id)
    {
        var cacheKey = $"lesson:{id}";
        
        // Пробуем получить из кэша
        var cachedData = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var lesson = JsonSerializer.Deserialize<LessonDto>(cachedData);
            if (lesson != null)
            {
                _logger.LogInformation("Retrieved lesson {Id} from cache", id);
                return lesson;
            }
        }

        // Получаем из БД
        var lessonEntity = await _context.Lessons.FindAsync(id);
        if (lessonEntity == null)
        {
            _logger.LogWarning("Lesson {Id} not found", id);
            return null;
        }

        var lessonDto = new LessonDto
        {
            Id = lessonEntity.Id,
            Title = lessonEntity.Title,
            Description = lessonEntity.Description,
            VideoId = lessonEntity.VideoId ?? Guid.Empty,
            VideoUrl = lessonEntity.VideoUrl,
            VideoStatus = lessonEntity.VideoStatus,
            CreatedAt = lessonEntity.CreatedAt,
            UpdatedAt = lessonEntity.UpdatedAt
        };

        // Сохраняем в кэш
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheDuration
        };
        
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(lessonDto), options);
        
        _logger.LogInformation("Retrieved lesson {Id} from database", id);
        return lessonDto;
    }

    public async Task<LessonDto> CreateLessonAsync(CreateLessonDto dto)
    {
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            VideoId = dto.VideoId,
            VideoStatus = dto.VideoId.HasValue ? VideoStatus.Uploading : VideoStatus.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync();

        // Очищаем кэш списка уроков
        await _cache.RemoveAsync("lessons:all");

        _logger.LogInformation("Created lesson {Id} with title {Title}", lesson.Id, lesson.Title);
        
        return new LessonDto
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Description = lesson.Description,
            VideoId = lesson.VideoId ?? Guid.Empty,
            VideoUrl = lesson.VideoUrl,
            VideoStatus = lesson.VideoStatus,
            CreatedAt = lesson.CreatedAt,
            UpdatedAt = lesson.UpdatedAt
        };
    }

    public async Task<LessonDto?> UpdateLessonAsync(Guid id, UpdateLessonDto dto)
    {
        var lesson = await _context.Lessons.FindAsync(id);
        if (lesson == null)
        {
            _logger.LogWarning("Lesson {Id} not found for update", id);
            return null;
        }

        if (!string.IsNullOrEmpty(dto.Title))
            lesson.Title = dto.Title;
        
        if (dto.Description != null)
            lesson.Description = dto.Description;
        
        if (dto.VideoId.HasValue)
        {
            lesson.VideoId = dto.VideoId.Value;
            lesson.VideoStatus = VideoStatus.Uploading;
        }

        await _context.SaveChangesAsync();

        // Очищаем кэш
        await _cache.RemoveAsync($"lesson:{id}");
        await _cache.RemoveAsync("lessons:all");

        _logger.LogInformation("Updated lesson {Id}", id);
        
        return await GetLessonByIdAsync(id);
    }

    public async Task<bool> DeleteLessonAsync(Guid id)
    {
        var lesson = await _context.Lessons.FindAsync(id);
        if (lesson == null)
        {
            _logger.LogWarning("Lesson {Id} not found for deletion", id);
            return false;
        }

        _context.Lessons.Remove(lesson);
        await _context.SaveChangesAsync();

        // Очищаем кэш
        await _cache.RemoveAsync($"lesson:{id}");
        await _cache.RemoveAsync("lessons:all");

        _logger.LogInformation("Deleted lesson {Id}", id);
        return true;
    }

    public async Task UpdateVideoStatusAsync(Guid lessonId, VideoStatus status, string? videoUrl = null)
    {
        var lesson = await _context.Lessons.FindAsync(lessonId);
        if (lesson == null)
        {
            _logger.LogWarning("Lesson {Id} not found for video status update", lessonId);
            return;
        }

        lesson.VideoStatus = status;
        if (!string.IsNullOrEmpty(videoUrl))
            lesson.VideoUrl = videoUrl;

        await _context.SaveChangesAsync();

        // Очищаем кэш
        await _cache.RemoveAsync($"lesson:{lessonId}");
        await _cache.RemoveAsync("lessons:all");

        _logger.LogInformation("Updated video status for lesson {Id} to {Status}", lessonId, status);
    }
}
