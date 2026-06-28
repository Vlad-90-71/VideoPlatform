namespace Shared.Configuration;

public class MinioSettings
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = string.Empty;
    public string PublicEndpoint { get; set; } = string.Empty;  // ✅ Добавьте
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string HlsBucketName { get; set; } = string.Empty;
    public bool WithSSL { get; set; }
    public int PresignedUrlExpirySeconds { get; set; } = 3600;  // ✅ Добавьте
}