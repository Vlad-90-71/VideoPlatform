using Minio.DataModel.Args;
using Shared.Models;
using System.Runtime;

namespace FileService.Services;

public interface IMinioService
{
    Task<string> GetPresignedUploadUrlAsync(string objectName, string contentType);
    Task<string> GetPresignedDownloadUrlAsync(string objectName, bool isHls = false);
    Task<string> GetPresignedDeleteUrlAsync(string objectName, bool isHls = false);

    Task<VideoInfoDto?> GetVideoInfoAsync(Guid videoId);
    Task<List<VideoInfoDto>> GetAllVideosAsync();
}

public class ObjectItem
{
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
