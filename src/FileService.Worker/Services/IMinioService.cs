namespace FileService.Worker.Services;

public interface IMinioService
{
    Task<Stream> GetObjectAsync(string objectName, bool fromHls = false);
    Task UploadObjectAsync(string objectName, Stream stream, string contentType, bool isHls = false);
    Task DeleteObjectAsync(string objectName, bool fromHls = false);
}
