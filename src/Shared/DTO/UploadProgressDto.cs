namespace Shared.DTO;

public class UploadProgressDto
{
    public Guid VideoId { get; set; }
    public int TotalChunks { get; set; }
    public int UploadedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)UploadedChunks / TotalChunks * 100 : 0;
    public bool IsCompleted => UploadedChunks >= TotalChunks;
}
