using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FileService.Worker.Services;

public partial class FFmpegService(ILogger<FFmpegService> logger) : IFFmpegService
{
    private readonly ILogger<FFmpegService> _logger = logger;

    [GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2})\.?(\d{0,2})")]
    private static partial Regex ProgressRegex();

    public async Task<string> ConvertToHlsAsync(string inputPath, string outputDir, IProgress<int>? progress = null)
    {
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var playlistPath = Path.Combine(outputDir, "playlist.m3u8");
        var segmentPattern = Path.Combine(outputDir, "segment_%03d.ts");

        // Получаем длительность видео
        var duration = await GetVideoDurationAsync(inputPath);

        // Экранируем пути для передачи в командную строку
        var escapedInputPath = $"\"{inputPath}\"";
        var escapedPlaylistPath = $"\"{playlistPath}\"";
        var escapedSegmentPattern = $"\"{segmentPattern}\"";

        // ✅ Команда FFmpeg — только видео и аудио, без субтитров
        var arguments = $"-i {escapedInputPath} " +
                "-map 0:v:0 -map 0:a:0 " +  // ← ДОБАВИТЬ ЭТУ СТРОКУ
                "-codec: copy " +
                "-start_number 0 " +
                "-hls_time 10 " +
                "-hls_list_size 0 " +
                $"-hls_segment_filename {escapedSegmentPattern} " +
                $"-f hls {escapedPlaylistPath}";

        var ffmpegPath = GetExecutablePath("ffmpeg");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

        var regex = ProgressRegex();
        var lastReportedProgress = 0;
        var errorOutput = new System.Text.StringBuilder();
        var errorReadComplete = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null)
            {
                errorReadComplete.TrySetResult(true);
                return;
            }

            errorOutput.AppendLine(e.Data);

            var match = regex.Match(e.Data);
            if (match.Success && duration > 0)
            {
                var hours = int.Parse(match.Groups[1].Value);
                var minutes = int.Parse(match.Groups[2].Value);
                var seconds = int.Parse(match.Groups[3].Value);
                var currentTime = hours * 3600 + minutes * 60 + seconds;

                var progressPercentage = Math.Min(100, (int)((currentTime / duration) * 100));

                if (progressPercentage > lastReportedProgress)
                {
                    lastReportedProgress = progressPercentage;
                    progress?.Report(progressPercentage);
                    _logger.LogDebug("FFmpeg progress: {Progress}%", progressPercentage);
                }
            }
        };

        _logger.LogInformation("Starting FFmpeg with arguments: {Arguments}", arguments);

        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        await errorReadComplete.Task;

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg failed with exit code {ExitCode}. Error: {Error}",
                process.ExitCode, errorOutput.ToString());
            throw new Exception($"FFmpeg failed with exit code {process.ExitCode}");
        }

        _logger.LogInformation("Converted video to HLS: {PlaylistPath}", playlistPath);
        return playlistPath;
    }

    private async Task<double> GetVideoDurationAsync(string inputPath)
    {
        var ffprobePath = GetExecutablePath("ffprobe");
        var escapedInputPath = $"\"{inputPath}\"";

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {escapedInputPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            _logger.LogWarning("Failed to start ffprobe process");
            return 0;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var duration))
        {
            _logger.LogInformation("Video duration: {Duration} seconds", duration);
            return duration;
        }

        _logger.LogWarning("Failed to parse video duration from output: {Output}", output);
        return 0;
    }

    /// <summary>
    /// Ищет исполняемый файл:
    /// 1. В PATH системы (предпочтительно для Docker)
    /// 2. В папке приложения (для Windows разработки)
    /// </summary>
    private string GetExecutablePath(string executableName)
    {
        // Определяем расширение для текущей ОС
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var executableFileName = isWindows ? $"{executableName}.exe" : executableName;

        // Вариант 1: Ищем рядом с приложением (для Windows разработки)
        var appDir = AppContext.BaseDirectory;
        var localPath = Path.Combine(appDir, executableFileName);

        if (File.Exists(localPath))
        {
            _logger.LogInformation("Found {Executable} in application directory: {Path}", executableFileName, localPath);
            return localPath;
        }

        // Вариант 2: Проверяем системные пути (для Docker)
        var systemPaths = new[] { "/usr/bin", "/usr/local/bin" };
        foreach (var systemPath in systemPaths)
        {
            var fullPath = Path.Combine(systemPath, executableFileName);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Found {Executable} in system path: {Path}", executableFileName, fullPath);
                return fullPath;
            }
        }

        // Вариант 3: Полагаемся на PATH (возвращаем просто имя файла)
        _logger.LogInformation("Using {Executable} from PATH", executableFileName);
        return executableFileName;
    }
}