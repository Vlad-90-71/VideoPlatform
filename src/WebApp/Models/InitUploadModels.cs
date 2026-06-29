namespace WebApp.Models;

public record InitUploadRequest
{
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public int ChunkSize { get; init; }
    public int TotalChunks { get; init; }
}

public record InitUploadResponse
{
    public Guid VideoId { get; init; }
    public int TotalChunks { get; init; }
    public int ChunkSize { get; init; }
    public List<ChunkUploadUrlDto> UploadUrls { get; init; } = [];
}

public record ChunkUploadUrlDto
{
    public int ChunkIndex { get; init; }
    public string UploadUrl { get; init; } = string.Empty;
}