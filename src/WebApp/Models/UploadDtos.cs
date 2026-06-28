namespace WebApp.Models;

public record InitUploadRequest(
    string FileName,
    long FileSize,
    int ChunkSize,
    int TotalChunks
);

public record InitUploadResponse(
    Guid VideoId,
    int TotalChunks,
    int ChunkSize,
    List<ChunkUploadUrlDto> UploadUrls
);

public record ChunkUploadUrlDto(
    int ChunkIndex,
    string UploadUrl
);