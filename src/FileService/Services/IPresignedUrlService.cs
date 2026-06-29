namespace FileService.Services;

public interface IPresignedUrlService
{
    // ✅ Presigned URLs - одиночные
    Task<string> GetPresignedUploadUrlAsync(
        string objectName,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600);

    Task<string> GetPresignedDownloadUrlAsync(
        string objectName,
        string bucketName,
        int expirySeconds = 3600);

    // ✅ Presigned URLs - пакетные
    Task<Dictionary<string, string>> GetPresignedUploadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        string contentType = "application/octet-stream",
        int expirySeconds = 3600);

    Task<Dictionary<string, string>> GetPresignedDownloadUrlsAsync(
        IEnumerable<string> objectNames,
        string bucketName,
        int expirySeconds = 3600);
}