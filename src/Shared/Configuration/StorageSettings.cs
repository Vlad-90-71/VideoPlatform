namespace Shared.Configuration;

public class StorageSettings
{
    public const string SectionName = "Storage";

    // Внутренний endpoint (для server-to-server)
    public string Endpoint { get; set; } = string.Empty;

    // ✅ Публичный endpoint (для presigned URLs, доступен из браузера)
    public string PublicEndpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public bool UseSSL { get; set; } = true;

    public string VideoStorageBucket { get; set; } = "video-storage";
    public string VideoHlsBucket { get; set; } = "video-hls";

    // ✅ Время жизни presigned URL (в секундах)
    public int PresignedUrlExpirySeconds { get; set; } = 3600; // 1 час
}