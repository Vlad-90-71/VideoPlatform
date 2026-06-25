using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Configuration;
using Shared.Constants;
using Shared.Messaging;
using Microsoft.Extensions.Options;

namespace FileService.Services;

public class RabbitMqService : IRabbitMqService, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqService> _logger;

    public RabbitMqService(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqService> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory()
        {
            HostName = settings.Value.Host,
            Port = settings.Value.Port,
            UserName = settings.Value.Username,
            Password = settings.Value.Password,
            VirtualHost = settings.Value.VirtualHost
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        await _channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.ProcessVideoExchange,
            type: ExchangeType.Direct,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: "video.process",
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: "video.process",
            exchange: MessagingConstants.ProcessVideoExchange,
            routingKey: MessagingConstants.ProcessVideoRoutingKey);
    }

    public async Task PublishProcessVideoCommandAsync(ProcessVideoCommand command)
    {
        var json = JsonSerializer.Serialize(command);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: MessagingConstants.ProcessVideoExchange,
            routingKey: MessagingConstants.ProcessVideoRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Published ProcessVideoCommand for video {VideoId}", command.VideoId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}