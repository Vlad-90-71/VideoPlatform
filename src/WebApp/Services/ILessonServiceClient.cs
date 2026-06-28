using Shared.DTO;

namespace WebApp.Services;

public interface ILessonServiceClient
{
    Task<IEnumerable<LessonDto>> GetAllLessonsAsync();
    Task<LessonDto?> GetLessonByIdAsync(Guid id);
    Task<LessonDto> CreateLessonAsync(string title, string description, Guid? videoId = null);
    Task<LessonDto?> UpdateLessonAsync(Guid id, string? title, string? description, Guid? videoId);
    Task<bool> DeleteLessonAsync(Guid id);
}
