using Shared.Messaging;

namespace FileService.Worker.Services;

public interface IRabbitMqService
{
    Task ConsumeProcessVideoQueueAsync(Func<ProcessVideoCommand, Task> handler);
    Task PublishVideoProgressAsync(VideoProgressEvent progressEvent);
}