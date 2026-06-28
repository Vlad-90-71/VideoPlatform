using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Configuration;
using Shared.Constants;
using Shared.Messaging;
using WebApp.Hubs;
using WebApp.Services;

namespace WebApp.Workers;

public class VideoProgressWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IVideoProgressCache progressCache,  // ✅ Добавлен кэш
    ILogger<VideoProgressWorker> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly RabbitMqSettings _settings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()!;
    private readonly IVideoProgressCache _progressCache = progressCache;  // ✅ Добавлено
    private readonly ILogger<VideoProgressWorker> _logger = logger;
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectToRabbitMqAsync();
        await StartConsumingAsync(stoppingToken);
    }

    private async Task ConnectToRabbitMqAsync()
    {
        var factory = new ConnectionFactory()
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.VideoProgressExchange,
            type: ExchangeType.Fanout,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: MessagingConstants.VideoProgressQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: MessagingConstants.VideoProgressQueue,
            exchange: MessagingConstants.VideoProgressExchange,
            routingKey: "");

        _logger.LogInformation("Connected to RabbitMQ and declared queue {Queue}", MessagingConstants.VideoProgressQueue);
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            _logger.LogError("Channel is not initialized");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var progressEvent = JsonSerializer.Deserialize<VideoProgressEvent>(json);

                if (progressEvent != null)
                {
                    _logger.LogInformation("Received progress event for video {VideoId}: {Progress}%",
                        progressEvent.VideoId, progressEvent.ProgressPercentage);

                    // ✅ СОХРАНЯЕМ прогресс в кэш
                    _progressCache.UpdateProgress(progressEvent);

                    using var scope = _serviceProvider.CreateScope();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<VideoProgressHub>>();

                    await hubContext.Clients.All
                        .SendAsync("ReceiveProgress", progressEvent, stoppingToken);

                    _logger.LogDebug("Broadcast progress event to all SignalR clients");

                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing video progress event");
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: MessagingConstants.VideoProgressQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Started consuming from queue {Queue}", MessagingConstants.VideoProgressQueue);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping VideoProgressWorker");

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken: cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken: cancellationToken);
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}