namespace LessonService.Services;

public class UpdateLessonDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? VideoId { get; set; }
}
