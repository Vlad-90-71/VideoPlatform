using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Configuration;
using Shared.Constants;
using Shared.Messaging;
using Microsoft.Extensions.Options;

namespace FileService.Worker.Services;

public class RabbitMqService(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqService> logger) : IRabbitMqService, IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly RabbitMqSettings _settings = settings.Value;
    private readonly ILogger<RabbitMqService> _logger = logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", _settings.Host, _settings.Port);

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

            // Инициализируем exchange и очереди
            await InitializeExchangesAndQueuesAsync();

            _initialized = true;
            _logger.LogInformation("Successfully connected to RabbitMQ");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeExchangesAndQueuesAsync()
    {
        // Гарантируем, что канал не null
        if (_channel is null)
        {
            throw new InvalidOperationException("RabbitMQ channel is not initialized");
        }

        // === ProcessVideo: Direct exchange ===
        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.ProcessVideoExchange,
            type: ExchangeType.Direct,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: MessagingConstants.ProcessVideoQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: MessagingConstants.ProcessVideoQueue,
            exchange: MessagingConstants.ProcessVideoExchange,
            routingKey: MessagingConstants.ProcessVideoRoutingKey);

        // === VideoProgress: Fanout exchange ===
        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.VideoProgressExchange,
            type: ExchangeType.Fanout,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: MessagingConstants.VideoProgressQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Fanout exchange ИГНОРИРУЕТ routing key
        await _channel.QueueBindAsync(
            queue: MessagingConstants.VideoProgressQueue,
            exchange: MessagingConstants.VideoProgressExchange,
            routingKey: "");
    }

    public async Task ConsumeProcessVideoQueueAsync(Func<ProcessVideoCommand, Task> handler)
    {
        await EnsureInitializedAsync();

        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var command = JsonSerializer.Deserialize<ProcessVideoCommand>(json);

                if (command != null)
                {
                    _logger.LogInformation("Received ProcessVideoCommand for video {VideoId}", command.VideoId);
                    await handler(command);
                    await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: MessagingConstants.ProcessVideoQueue,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Started consuming from queue {Queue}", MessagingConstants.ProcessVideoQueue);
    }

    public async Task PublishVideoProgressAsync(VideoProgressEvent progressEvent)
    {
        await EnsureInitializedAsync();

        var json = JsonSerializer.Serialize(progressEvent);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // Fanout exchange — routing key не важен
        await _channel!.BasicPublishAsync(
            exchange: MessagingConstants.VideoProgressExchange,
            routingKey: "",
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Published VideoProgressEvent for video {VideoId}: {Progress}%",
            progressEvent.VideoId, progressEvent.ProgressPercentage);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing channel");
            }
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing connection");
            }
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}