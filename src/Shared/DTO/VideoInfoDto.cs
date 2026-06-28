namespace Shared.Models;

public class VideoInfoDto
{
    public Guid VideoId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string HlsPlaylistUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public DateTime CreatedAt { get; set; }
}