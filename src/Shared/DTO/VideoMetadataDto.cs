namespace Shared.DTO;

public class VideoMetadataDto
{
    public Guid VideoId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int UploadedChunks { get; set; }
    public VideoStatus Status { get; set; }
    public string? HlsPlaylistUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
