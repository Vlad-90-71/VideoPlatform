namespace FileService.Models;

public class ChunkUploadRequest
{
    public Guid VideoId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public IFormFile File { get; set; } = null!;
}
