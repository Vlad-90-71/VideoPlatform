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

public enum VideoProcessingStatus
{
    Started,
    Processing,
    Completed,
    Failed
}
