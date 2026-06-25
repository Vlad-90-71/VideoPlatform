namespace Shared.Messaging;

public class ProcessVideoCommand
{
    public Guid VideoId { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
