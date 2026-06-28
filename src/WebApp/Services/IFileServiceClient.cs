using Shared.DTO;
using Shared.Models;
using WebApp.Models;

namespace WebApp.Services;

public interface IFileServiceClient
{
    Task<UploadProgressDto> UploadChunkAsync(Guid videoId, string fileName, int chunkIndex, int totalChunks, Stream chunkStream);
    Task<VideoMetadataDto> CompleteUploadAsync(Guid videoId, string fileName, int totalChunks);
    //Task<VideoMetadataDto> GetVideoMetadataAsync(Guid videoId);

    Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId);
    Task<List<VideoInfoDto>> GetAllVideosAsync();

    Task<InitUploadResponse> InitUploadAsync(InitUploadRequest request);
}
