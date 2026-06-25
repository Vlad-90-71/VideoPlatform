namespace Shared.Configuration;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";
    
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    
    public string ProcessVideoQueue { get; set; } = "video.process";
    public string VideoProgressQueue { get; set; } = "video.progress";
}
