using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Configuration;
using Shared.Constants;
using Shared.Messaging;
using Microsoft.Extensions.Options;

namespace FileService.Worker.Services;

public class RabbitMqService : IRabbitMqService, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqService> _logger;

    public RabbitMqService(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var factory = new ConnectionFactory()
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        InitializeExchangesAndQueuesAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeExchangesAndQueuesAsync()
    {
        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.ProcessVideoExchange,
            type: ExchangeType.Direct,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: _settings.ProcessVideoQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: _settings.ProcessVideoQueue,
            exchange: MessagingConstants.ProcessVideoExchange,
            routingKey: MessagingConstants.ProcessVideoRoutingKey);

        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.VideoProgressExchange,
            type: ExchangeType.Fanout,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: _settings.VideoProgressQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: _settings.VideoProgressQueue,
            exchange: MessagingConstants.VideoProgressExchange,
            routingKey: MessagingConstants.VideoProgressRoutingKey);
    }

    public void ConsumeProcessVideoQueue(Func<ProcessVideoCommand, Task> handler)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        // ✅ В v7 используем ReceivedAsync вместо Received
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
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _ = StartConsumerAsync(consumer);
    }

    private async Task StartConsumerAsync(AsyncEventingBasicConsumer consumer)
    {
        await _channel.BasicConsumeAsync(
            queue: _settings.ProcessVideoQueue,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Started consuming from queue {Queue}", _settings.ProcessVideoQueue);
    }

    public async Task PublishVideoProgressAsync(VideoProgressEvent progressEvent)
    {
        var json = JsonSerializer.Serialize(progressEvent);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: MessagingConstants.VideoProgressExchange,
            routingKey: MessagingConstants.VideoProgressRoutingKey,
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