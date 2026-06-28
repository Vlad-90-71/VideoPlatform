using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using WebApp.Common;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

// ✅ ДОБАВЬТЕ: явный маршрут для контроллера
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

    // ✅ ИСПРАВЛЕНО: явный маршрут для GET
    [HttpGet("Upload")]
    public IActionResult Upload()
    {
        ViewBag.MaxFileSizeMB = AppConstants.MaxFileSizeMB;
        ViewBag.ChunkSizeMB = AppConstants.ChunkSizeMB;
        return View();
    }

    // ✅ ИСПРАВЛЕНО: явный маршрут
    [HttpPost("InitUpload")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> InitUpload([FromBody] InitUploadRequest request)
    {
        _logger.LogInformation("InitUpload called: {FileName}, {TotalChunks}",
            request.FileName, request.TotalChunks);

        try
        {
            var response = await _fileService.InitUploadAsync(request);

            if (response == null)
            {
                _logger.LogError("InitUploadAsync returned null");
                return BadRequest(new { error = "Failed to initialize upload" });
            }

            _logger.LogInformation("InitUpload succeeded: VideoId={VideoId}", response.VideoId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing upload");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ✅ ИСПРАВЛЕНО: явный маршрут
    [HttpPost("CompleteUpload")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest request)
    {
        try
        {
            // ✅ Вызываем VideoService в WebApp (не FileService!)
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
            // ✅ Используем кэш прогресса вместо FileService
            var progress = _progressCache.GetProgress(videoId);

            if (progress == null)
            {
                // Если прогресса нет — показываем страницу с ожиданием
                ViewBag.VideoId = videoId;
                ViewBag.HlsPlaylistUrl = null;
                ViewBag.FileName = "Обработка видео...";
                return View();
            }

            ViewBag.VideoId = videoId;
            ViewBag.HlsPlaylistUrl = progress.HlsPlaylistUrl;
            ViewBag.FileName = "Видео - " + videoId.ToString();
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
    // ✅ ИСПРАВЛЕНО: явный маршрут для Index
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var videos = await _fileService.GetAllVideosAsync();
            return View(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading videos list");
            return View(new List<VideoInfoDto>());
        }
    }
}