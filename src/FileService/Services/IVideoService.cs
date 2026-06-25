using FileService.Models;
using Shared.DTO;

namespace FileService.Services;

public interface IVideoService
{
    Task<UploadProgressDto> UploadChunkAsync(ChunkUploadRequest request);
    Task<VideoMetadataDto> CompleteUploadAsync(UploadCompleteRequest request);
    Task<VideoMetadataDto> GetVideoMetadataAsync(Guid videoId);
}
