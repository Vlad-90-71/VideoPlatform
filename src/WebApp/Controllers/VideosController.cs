using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using WebApp.Common;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

// ✅ ДОБАВЬТЕ: явный маршрут для контроллера
[Route("[controller]")]
public class VideosController(IFileServiceClient fileService, ILogger<VideosController> logger) : Controller
{
    private readonly IFileServiceClient _fileService = fileService;
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
    [HttpPost("UploadChunk")]
    [RequestSizeLimit(AppConstants.MaxFileSizeBytes)]
    public async Task<IActionResult> UploadChunk(IFormFile file, Guid videoId, int chunkIndex, int totalChunks)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var progress = await _fileService.UploadChunkAsync(videoId, file.FileName, chunkIndex, totalChunks, stream);

            return Ok(new
            {
                success = true,
                progress = progress.ProgressPercentage,
                isCompleted = progress.IsCompleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chunk {ChunkIndex} for video {VideoId}", chunkIndex, videoId);
            return BadRequest(new { success = false, error = ex.Message });
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
            var metadata = await _fileService.CompleteUploadAsync(request.VideoId, request.FileName, request.TotalChunks);
            return Ok(new { success = true, videoId = metadata.VideoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing upload for video {VideoId}", request.VideoId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("Watch/{videoId}")]
    public async Task<IActionResult> Watch(Guid videoId)
    {
        try
        {
            var videoInfo = await _fileService.GetVideoInfoAsync(videoId);

            if (videoInfo == null || string.IsNullOrEmpty(videoInfo.HlsPlaylistUrl))
            {
                return NotFound("Видео не найдено или ещё не обработано");
            }

            ViewBag.VideoId = videoId;
            ViewBag.HlsPlaylistUrl = videoInfo.HlsPlaylistUrl;
            ViewBag.FileName = videoInfo.FileName;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading video {VideoId}", videoId);
            return NotFound("Ошибка загрузки видео");
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