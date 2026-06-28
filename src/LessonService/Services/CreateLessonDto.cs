namespace LessonService.Services;

public class CreateLessonDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? VideoId { get; set; }
}
