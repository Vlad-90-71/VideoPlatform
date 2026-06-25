namespace FileService.Worker.Services;

public interface IMinioService
{
    Task<Stream> GetObjectAsync(string objectName);
    Task UploadObjectAsync(string objectName, Stream stream, string contentType);
    Task DeleteObjectAsync(string objectName);
}
