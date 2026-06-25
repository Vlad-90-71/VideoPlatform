namespace Shared.DTO;

public class LessonDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid VideoId { get; set; }
    public string? VideoUrl { get; set; }
    public VideoStatus VideoStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum VideoStatus
{
    Uploading,
    Processing,
    Ready,
    Failed
}
