using FileService.Models;
using Shared.DTO;
using Shared.Models;

namespace FileService.Services;

public interface IVideoService
{
    Task<UploadProgressDto> UploadChunkAsync(ChunkUploadRequest request);
    Task<VideoMetadataDto> CompleteUploadAsync(UploadCompleteRequest request);
    Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId);
    Task<List<VideoInfoDto>> GetAllVideosAsync();
    Task<string> GetPresignedUploadUrlAsync(string objectName);
}
