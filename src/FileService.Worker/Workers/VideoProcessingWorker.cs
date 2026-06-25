using FileService.Worker.Services;

namespace FileService.Worker.Workers;

public class VideoProcessingWorker(
    IRabbitMqService rabbitMqService,
    IVideoProcessingService videoProcessingService,
    ILogger<VideoProcessingWorker> logger) : BackgroundService
{
    private readonly IRabbitMqService _rabbitMqService = rabbitMqService;
    private readonly IVideoProcessingService _videoProcessingService = videoProcessingService;
    private readonly ILogger<VideoProcessingWorker> _logger = logger;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video Processing Worker started");

        _rabbitMqService.ConsumeProcessVideoQueue(async command =>
        {
            try
            {
                await _videoProcessingService.ProcessVideoAsync(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video {VideoId}", command.VideoId);
            }
        });

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Video Processing Worker stopping");
        return base.StopAsync(cancellationToken);
    }
}
