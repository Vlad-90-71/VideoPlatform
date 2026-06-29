using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using WebApp.Common;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

[Route("[controller]")]
public class VideosController(
    IFileServiceClient fileService,
    IVideoService videoService,
    IVideoProgressCache progressCache,
    ILogger<VideosController> logger) : Controller
{
    private readonly IFileServiceClient _fileService = fileService;
    private readonly IVideoService _videoService = videoService;
    private readonly IVideoProgressCache _progressCache = progressCache;
    private readonly ILogger<VideosController> _logger = logger;

    private const string VideoStorageBucket = "video-storage";
    private const string VideoHlsBucket = "video-hls";

    [HttpGet("Upload")]
    public IActionResult Upload()
    {
        ViewBag.MaxFileSizeMB = AppConstants.MaxFileSizeMB;
        ViewBag.ChunkSizeMB = AppConstants.ChunkSizeMB;
        return View();
    }

    // ✅ ИСПРАВЛЕНО: используем новый API FileService
    [HttpPost("InitUpload")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> InitUpload([FromBody] InitUploadRequest request)
    {
        _logger.LogInformation("InitUpload called: {FileName}, {TotalChunks}",
            request.FileName, request.TotalChunks);

        try
        {
            // ✅ Генерируем videoId
            var videoId = Guid.NewGuid();

            // ✅ Формируем имена объектов для чанков
            var chunkObjectNames = Enumerable.Range(0, request.TotalChunks)
                .Select(i => $"{videoId}/chunks/chunk_{i:D6}")
                .ToList();

            // ✅ Запрашиваем presigned URLs у FileService
            var uploadUrls = await _fileService.GetPresignedUploadUrlsAsync(
                chunkObjectNames,
                VideoStorageBucket,
                "application/octet-stream",
                expirySeconds: 3600);

            // ✅ Формируем ответ
            var response = new InitUploadResponse
            {
                VideoId = videoId,
                TotalChunks = request.TotalChunks,
                ChunkSize = request.ChunkSize,
                UploadUrls = [.. uploadUrls.Select((url, index) => new ChunkUploadUrlDto
                {
                    ChunkIndex = index,
                    UploadUrl = url.Value
                })]
            };

            _logger.LogInformation("InitUpload succeeded: VideoId={VideoId}, {Count} URLs generated",
                videoId, uploadUrls.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing upload");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("CompleteUpload")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest request)
    {
        try
        {
            // ✅ Вызываем VideoService в WebApp (публикует в RabbitMQ)
            var metadata = await _videoService.CompleteUploadAsync(
                request.VideoId,
                request.FileName,
                request.TotalChunks);

            return Ok(new { success = true, videoId = metadata.VideoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing upload for video {VideoId}", request.VideoId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("Watch/{videoId}")]
    public IActionResult Watch(Guid videoId)
    {
        try
        {
            var progress = _progressCache.GetProgress(videoId);

            if (progress == null)
            {
                ViewBag.VideoId = videoId;
                ViewBag.HlsPlaylistUrl = null;
                ViewBag.FileName = "Обработка видео...";
                return View();
            }

            ViewBag.VideoId = videoId;
            ViewBag.HlsPlaylistUrl = progress.HlsPlaylistUrl;
            ViewBag.FileName = progress.FileName ?? $"Видео - {videoId.ToString()[..8]}";
            ViewBag.ProgressPercentage = progress.ProgressPercentage;
            ViewBag.Status = (int)progress.Status;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading video {VideoId}", videoId);
            return NotFound("Ошибка загрузки видео");
        }
    }

    [HttpGet("GetVideoStatus/{videoId}")]
    public IActionResult GetVideoStatus(Guid videoId)
    {
        try
        {
            var progress = _progressCache.GetProgress(videoId);

            if (progress == null)
            {
                return Ok(new
                {
                    isCompleted = false,
                    progressPercentage = 0,
                    status = 0
                });
            }

            return Ok(new
            {
                isCompleted = progress.Status == Shared.Messaging.VideoProcessingStatus.Completed,
                progressPercentage = progress.ProgressPercentage,
                status = (int)progress.Status,
                hlsPlaylistUrl = progress.HlsPlaylistUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video status for {VideoId}", videoId);
            return Ok(new
            {
                isCompleted = false,
                progressPercentage = 0,
                status = 0
            });
        }
    }

    // ✅ ИСПРАВЛЕНО: используем новый API FileService
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        try
        {
            // ✅ Получаем список HLS объектов
            var hlsObjects = await _fileService.ListObjectsAsync(VideoHlsBucket, recursive: true);

            // ✅ Группируем по videoId (ищем playlist.m3u8)
            var videoGroups = hlsObjects
                .Where(o => o.Key.EndsWith("/hls/playlist.m3u8"))
                .Select(o => o.Key.Split('/').First())
                .Where(id => Guid.TryParse(id, out _))
                .Distinct()
                .ToList();

            var videos = new List<VideoInfoDto>();

            foreach (var videoIdStr in videoGroups)
            {
                if (Guid.TryParse(videoIdStr, out var videoId))
                {
                    var playlistObject = hlsObjects.FirstOrDefault(o => o.Key == $"{videoId}/hls/playlist.m3u8");

                    if (playlistObject != null)
                    {
                        videos.Add(new VideoInfoDto
                        {
                            VideoId = videoId,
                            FileName = $"Video_{videoId.ToString()[..8]}",
                            HlsPlaylistUrl = $"{videoId}/hls/playlist.m3u8",
                            Status = "Completed",
                            CreatedAt = playlistObject.LastModified
                        });
                    }
                }
            }

            _logger.LogInformation("Found {Count} videos", videos.Count);
            return View(videos.OrderByDescending(v => v.CreatedAt).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading videos list");
            return View(new List<VideoInfoDto>());
        }
    }
}