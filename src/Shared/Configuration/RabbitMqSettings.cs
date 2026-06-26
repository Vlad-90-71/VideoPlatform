namespace Shared.Configuration;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "vp_rabbit";
    public string Password { get; set; } = "vp_rabbit_secret";
    public string VirtualHost { get; set; } = "/";
}