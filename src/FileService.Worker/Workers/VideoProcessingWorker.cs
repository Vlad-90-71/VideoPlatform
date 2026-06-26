using FileService.Worker.Services;

namespace FileService.Worker.Workers;

public class VideoProcessingWorker(
    IRabbitMqService rabbitMqService,
    IServiceScopeFactory scopeFactory,
    ILogger<VideoProcessingWorker> logger) : BackgroundService
{
    private readonly IRabbitMqService _rabbitMqService = rabbitMqService;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<VideoProcessingWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video Processing Worker started");

        // ✅ Правильное имя метода: ConsumeProcessVideoQueueAsync
        await _rabbitMqService.ConsumeProcessVideoQueueAsync(async command =>
        {
            // Создаём scope для каждого сообщения из очереди
            using var scope = _scopeFactory.CreateScope();
            var videoProcessingService = scope.ServiceProvider.GetRequiredService<IVideoProcessingService>();

            try
            {
                await videoProcessingService.ProcessVideoAsync(command);
                _logger.LogInformation("Successfully processed video {VideoId}", command.VideoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video {VideoId}", command.VideoId);
            }
        });

        // ✅ Ждём сигнала остановки
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Video Processing Worker stopping");
        return base.StopAsync(cancellationToken);
    }
}