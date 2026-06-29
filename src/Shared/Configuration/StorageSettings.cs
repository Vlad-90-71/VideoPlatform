namespace Shared.Configuration;

public class StorageSettings
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = string.Empty;
    public string PublicEndpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public bool UseSSL { get; set; } = true;

    public string VideoStorageBucket { get; set; } = "video-storage";
    public string VideoHlsBucket { get; set; } = "video-hls";

    public int PresignedUrlExpirySeconds { get; set; } = 3600;
}