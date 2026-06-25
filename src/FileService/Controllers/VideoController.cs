using FileService.Models;
using FileService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController(IVideoService videoService, ILogger<VideoController> logger) : ControllerBase
{
    private readonly IVideoService _videoService = videoService;
    private readonly ILogger<VideoController> _logger = logger;

    [HttpPost("upload/chunk")]
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

    [HttpGet("{videoId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideoMetadata(Guid videoId)
    {
        try
        {
            var metadata = await _videoService.GetVideoMetadataAsync(videoId);
            return Ok(metadata);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
