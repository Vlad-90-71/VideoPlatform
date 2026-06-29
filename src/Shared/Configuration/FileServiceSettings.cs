namespace Shared.Configuration;

public class FileServiceSettings
{
    public const string SectionName = "FileService";

    public string BaseUrl { get; set; } = "http://fileservice:8080";
}