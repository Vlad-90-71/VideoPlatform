namespace FileService.Models;

public class UploadCompleteRequest
{
    public Guid VideoId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
}
