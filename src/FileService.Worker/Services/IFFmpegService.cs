namespace FileService.Worker.Services;

public interface IFFmpegService
{
    Task<string> ConvertToHlsAsync(string inputPath, string outputDir, IProgress<int>? progress = null);
}
