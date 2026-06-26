using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Configuration;
using Shared.Constants;
using Shared.Messaging;
using Microsoft.Extensions.Options;

namespace FileService.Services;

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

            // Producer создаёт только exchange (очереди создаёт consumer)
            await _channel.ExchangeDeclareAsync(
                exchange: MessagingConstants.ProcessVideoExchange,
                type: ExchangeType.Direct,
                durable: true);

            _initialized = true;
            _logger.LogInformation("Successfully connected to RabbitMQ");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PublishProcessVideoCommandAsync(ProcessVideoCommand command)
    {
        await EnsureInitializedAsync();

        var json = JsonSerializer.Serialize(command);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel!.BasicPublishAsync(
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