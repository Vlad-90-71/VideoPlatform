using Microsoft.AspNetCore.Mvc;
using FileService.Services;

namespace FileService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController(IMinioService minioService, ILogger<VideoController> logger) : ControllerBase
{
    private readonly IMinioService _minioService = minioService;
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
                var presignedUrl = await _minioService.GetPresignedUploadUrlAsync(objectName, "application/octet-stream");

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

    // ✅ Оставили только один метод
    [HttpGet("{videoId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideoInfo(Guid videoId)
    {
        try
        {
            var videoInfo = await _minioService.GetVideoInfoAsync(videoId);

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
            var videos = await _minioService.GetAllVideosAsync();
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all videos");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}