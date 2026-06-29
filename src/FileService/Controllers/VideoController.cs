using Microsoft.AspNetCore.Mvc;
using FileService.Services;

namespace FileService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController(
    IPresignedUrlService presignedUrlService,
    IObjectStorageService objectStorageService,
    ILogger<VideoController> logger) : ControllerBase
{
    private readonly IPresignedUrlService _presignedUrlService = presignedUrlService;
    private readonly IObjectStorageService _objectStorageService = objectStorageService;
    private readonly ILogger<VideoController> _logger = logger;

    #region Presigned URLs - Upload

    [HttpPost("presigned/upload")]
    public async Task<IActionResult> GetPresignedUploadUrls([FromBody] PresignedUrlsRequest request)
    {
        try
        {
            _logger.LogInformation("Generating {Count} presigned upload URLs for bucket {Bucket}",
                request.ObjectNames.Count(), request.BucketName);

            var urls = await _presignedUrlService.GetPresignedUploadUrlsAsync(
                request.ObjectNames,
                request.BucketName,
                request.ContentType,
                request.ExpirySeconds);

            return Ok(new PresignedUrlsResponse
            {
                Urls = urls,
                ExpirySeconds = request.ExpirySeconds,
                Operation = "upload"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned upload URLs");
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Presigned URLs - Download

    [HttpPost("presigned/download")]
    public async Task<IActionResult> GetPresignedDownloadUrls([FromBody] PresignedUrlsRequest request)
    {
        try
        {
            _logger.LogInformation("Generating {Count} presigned download URLs for bucket {Bucket}",
                request.ObjectNames.Count(), request.BucketName);

            var urls = await _presignedUrlService.GetPresignedDownloadUrlsAsync(
                request.ObjectNames,
                request.BucketName,
                request.ExpirySeconds);

            return Ok(new PresignedUrlsResponse
            {
                Urls = urls,
                ExpirySeconds = request.ExpirySeconds,
                Operation = "download"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned download URLs");
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Direct Delete

    [HttpDelete("objects")]
    public async Task<IActionResult> DeleteObjects([FromBody] DeleteObjectsRequest request)
    {
        try
        {
            _logger.LogInformation("Deleting {Count} objects from bucket {Bucket}",
                request.ObjectNames.Count(), request.BucketName);

            await _objectStorageService.DeleteObjectsAsync(request.ObjectNames, request.BucketName);

            return Ok(new
            {
                success = true,
                deletedCount = request.ObjectNames.Count(),
                bucketName = request.BucketName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting objects from bucket {Bucket}", request.BucketName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region List Objects

    [HttpGet("objects")]
    public async Task<IActionResult> ListObjects(
        [FromQuery] string bucketName,
        [FromQuery] string? prefix = null,
        [FromQuery] bool recursive = true)
    {
        try
        {
            _logger.LogInformation("Listing objects in bucket {Bucket} with prefix {Prefix}",
                bucketName, prefix);

            var objects = await _objectStorageService.ListObjectsAsync(bucketName, prefix, recursive);

            return Ok(new
            {
                bucketName,
                prefix,
                count = objects.Count,
                objects
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing objects in bucket {Bucket}", bucketName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region DTOs

    public record PresignedUrlsRequest
    {
        public IEnumerable<string> ObjectNames { get; init; } = [];
        public string BucketName { get; init; } = string.Empty;
        public int ExpirySeconds { get; init; } = 3600;
        public string ContentType { get; init; } = "application/octet-stream";
    }

    public record PresignedUrlsResponse
    {
        public Dictionary<string, string> Urls { get; init; } = [];
        public int ExpirySeconds { get; init; }
        public string Operation { get; init; } = string.Empty;
    }

    public record DeleteObjectsRequest
    {
        public IEnumerable<string> ObjectNames { get; init; } = [];
        public string BucketName { get; init; } = string.Empty;
    }

    #endregion
}