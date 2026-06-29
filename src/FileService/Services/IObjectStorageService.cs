using FileService.Models;

namespace FileService.Services;

public interface IObjectStorageService
{
    // ✅ Операции с объектами
    Task<Stream> GetObjectAsync(string objectName, string bucketName);
    Task UploadObjectAsync(string objectName, Stream stream, string bucketName, string contentType);
    Task DeleteObjectAsync(string objectName, string bucketName);
    Task DeleteObjectsAsync(IEnumerable<string> objectNames, string bucketName);

    // ✅ Список объектов
    Task<List<ObjectItem>> ListObjectsAsync(string bucketName, string? prefix = null, bool recursive = true);

    // ✅ Проверка существования
    Task<bool> ObjectExistsAsync(string objectName, string bucketName);
}