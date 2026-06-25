namespace FileService.Services;

public interface IMinioService
{
    Task<string> UploadChunkAsync(Guid videoId, int chunkIndex, Stream chunkStream, string fileName);
    Task<string> MergeChunksAsync(Guid videoId, int totalChunks, string fileName);
    Task<Stream> GetVideoStreamAsync(string objectName);
    Task DeleteVideoAsync(string objectName);
}
