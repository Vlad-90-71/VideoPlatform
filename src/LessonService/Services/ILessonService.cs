using Shared.DTO;

namespace LessonService.Services;

public interface ILessonService
{
    Task<IEnumerable<LessonDto>> GetAllLessonsAsync();
    Task<LessonDto?> GetLessonByIdAsync(Guid id);
    Task<LessonDto> CreateLessonAsync(CreateLessonDto dto);
    Task<LessonDto?> UpdateLessonAsync(Guid id, UpdateLessonDto dto);
    Task<bool> DeleteLessonAsync(Guid id);
    Task UpdateVideoStatusAsync(Guid lessonId, VideoStatus status, string? videoUrl = null);
}
