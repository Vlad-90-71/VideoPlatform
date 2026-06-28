namespace FileService.Services;

public interface IMinioService
{
    Task<string> UploadChunkAsync(Guid videoId, int chunkIndex, Stream chunkStream, string fileName);
    Task<Stream> GetObjectAsync(string objectName, bool fromHls = false);
    Task UploadObjectAsync(string objectName, Stream stream, string contentType, bool isHls = false);
    Task DeleteObjectAsync(string objectName, bool fromHls = false);
    Task<List<ObjectItem>> ListObjectsAsync(string prefix, bool isHls = false);
    Task<string> GetPresignedUploadUrlAsync(string objectName, string contentType);
}
public class ObjectItem
{
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
