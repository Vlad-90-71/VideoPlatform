using FileService.Models;
using FileService.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Models;

namespace FileService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController(IVideoService videoService, ILogger<VideoController> logger) : ControllerBase
{
    private readonly IVideoService _videoService = videoService;
    private readonly ILogger<VideoController> _logger = logger;


    // ✅ Новый endpoint — инициализация загрузки и получение presigned URLs
    [HttpPost("upload/init")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitUpload([FromBody] InitUploadRequest request)
    {
        try
        {
            var videoId = Guid.NewGuid();
            var uploadUrls = new List<ChunkUploadUrlDto>();

            // Генерируем presigned URL для каждого чанка
            for (int i = 0; i < request.TotalChunks; i++)
            {
                var objectName = $"{videoId}/chunks/chunk_{i:D6}";
                var presignedUrl = await _videoService.GetPresignedUploadUrlAsync(objectName);

                uploadUrls.Add(new ChunkUploadUrlDto(i, presignedUrl));
            }

            var response = new InitUploadResponse
            (
                videoId,
                request.TotalChunks,
                request.ChunkSize,
                 uploadUrls
            );

            _logger.LogInformation("Initialized upload for video {VideoId}, {TotalChunks} chunks",
                videoId, request.TotalChunks);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing upload");
            return BadRequest(new { error = ex.Message });
        }
    }

    // DTO для запроса
    public record InitUploadRequest(
        string FileName,
        long FileSize,
        int ChunkSize,
        int TotalChunks
    );

    // DTO для ответа
    public record InitUploadResponse(
        Guid VideoId,
        int TotalChunks,
        int ChunkSize,
        List<ChunkUploadUrlDto> UploadUrls
    );

    public record ChunkUploadUrlDto(
        int ChunkIndex,
        string UploadUrl
    );

    [HttpPost("upload/chunk")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadChunk([FromForm] ChunkUploadRequest request)
    {
        try
        {
            var progress = await _videoService.UploadChunkAsync(request);
            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chunk for video {VideoId}", request.VideoId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("upload/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteUpload([FromBody] UploadCompleteRequest request)
    {
        try
        {
            var metadata = await _videoService.CompleteUploadAsync(request);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing upload for video {VideoId}", request.VideoId);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ✅ Оставили только один метод
    [HttpGet("{videoId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideoInfo(Guid videoId)
    {
        try
        {
            var videoInfo = await _videoService.GetVideoInfoAsync(videoId);

            if (videoInfo == null)
            {
                return NotFound();
            }

            return Ok(videoInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video info for {VideoId}", videoId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("list")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllVideos()
    {
        try
        {
            var videos = await _videoService.GetAllVideosAsync();
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all videos");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}