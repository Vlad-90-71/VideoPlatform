using Minio;
using Minio.DataModel.Args;
using Shared.Configuration;
using Microsoft.Extensions.Options;

namespace FileService.Services;

public class MinioService(IMinioClient minioClient, IOptions<MinioSettings> settings, ILogger<MinioService> logger) : IMinioService
{
    private readonly IMinioClient _minioClient = minioClient;
    private readonly MinioSettings _settings = settings.Value;
    private readonly ILogger<MinioService> _logger = logger;

    public async Task<string> UploadChunkAsync(Guid videoId, int chunkIndex, Stream chunkStream, string fileName)
    {
        var objectName = $"{videoId}/chunks/chunk_{chunkIndex:D6}";

        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithStreamData(chunkStream)
            .WithObjectSize(chunkStream.Length)
            .WithContentType("application/octet-stream"));

        _logger.LogInformation("Uploaded chunk {ChunkIndex} for video {VideoId}", chunkIndex, videoId);
        return objectName;
    }

    public async Task<string> MergeChunksAsync(Guid videoId, int totalChunks, string fileName)
    {
        var finalObjectName = $"{videoId}/{fileName}";
        var tempFileName = Path.GetTempFileName();

        try
        {
            // Скачиваем все чанки и объединяем локально
            using (var finalStream = new FileStream(tempFileName, FileMode.Create))
            {
                for (int i = 0; i < totalChunks; i++)
                {
                    var chunkObjectName = $"{videoId}/chunks/chunk_{i:D6}";

                    // Используем callback для получения данных
                    await _minioClient.GetObjectAsync(new GetObjectArgs()
                        .WithBucket(_settings.BucketName)
                        .WithObject(chunkObjectName)
                        .WithCallbackStream(async (stream, cancellationToken) =>
                        {
                            await stream.CopyToAsync(finalStream, cancellationToken);
                        }));
                }
            }

            // Загружаем объединённый файл в MinIO
            using (var fileStream = new FileStream(tempFileName, FileMode.Open))
            {
                await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(finalObjectName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileStream.Length)
                    .WithContentType("video/mp4"));
            }

            // Удаляем чанки
            for (int i = 0; i < totalChunks; i++)
            {
                var chunkObjectName = $"{videoId}/chunks/chunk_{i:D6}";
                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(chunkObjectName));
            }

            _logger.LogInformation("Merged {TotalChunks} chunks for video {VideoId}", totalChunks, videoId);
            return finalObjectName;
        }
        finally
        {
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
    }

    public async Task<Stream> GetVideoStreamAsync(string objectName)
    {
        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            }));

        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteVideoAsync(string objectName)
    {
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName));
    }
}