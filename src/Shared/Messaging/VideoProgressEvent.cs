namespace Shared.Messaging;

public class VideoProgressEvent
{
    public Guid VideoId { get; set; }
    public int ProgressPercentage { get; set; }
    public VideoProcessingStatus Status { get; set; }
    public string FileName { get; init; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? HlsPlaylistUrl { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum VideoProcessingStatus
{
    NotStarted = 0,
    Started = 1,
    Processing = 2,
    Completed = 3,  
    Failed = 4
}