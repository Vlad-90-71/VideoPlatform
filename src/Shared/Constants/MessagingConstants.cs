namespace Shared.Constants;

public static class MessagingConstants
{
    // Exchanges
    public const string ProcessVideoExchange = "video.processing.exchange";
    public const string VideoProgressExchange = "video.progress.exchange";

    // Routing Keys
    public const string ProcessVideoRoutingKey = "video.process";
    public const string VideoProgressRoutingKey = "video.progress";

    // Queues (единые имена для всех сервисов)
    public const string ProcessVideoQueue = "video.process.queue";
    public const string VideoProgressQueue = "video.progress.queue";
}