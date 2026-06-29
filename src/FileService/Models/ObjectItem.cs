namespace FileService.Models;

public record ObjectItem
{
    public string Key { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string ETag { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}