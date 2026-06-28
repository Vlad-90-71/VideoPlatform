namespace LessonService.Models;

public class Lesson
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? VideoId { get; set; }
    public string? VideoUrl { get; set; }
    public Shared.DTO.VideoStatus VideoStatus { get; set; } = Shared.DTO.VideoStatus.Uploading;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
