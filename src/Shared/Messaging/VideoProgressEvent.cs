namespace Shared.Messaging;

public class VideoProgressEvent
{
    public Guid VideoId { get; set; }
    public int ProgressPercentage { get; set; }
    public VideoProcessingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? HlsPlaylistUrl { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum VideoProcessingStatus1
{
    Started,
    Processing,
    Completed,
    Failed
}
public enum VideoProcessingStatus
{
    NotStarted = 0,
    Started = 1,
    Processing = 2,
    Completed = 3,  // ✅ Должно быть 3, а не 2!
    Failed = 4
}
