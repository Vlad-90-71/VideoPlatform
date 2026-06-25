using Shared.Messaging;

namespace FileService.Worker.Services;

public interface IRabbitMqService
{
    void ConsumeProcessVideoQueue(Func<Shared.Messaging.ProcessVideoCommand, Task> handler);
    Task PublishVideoProgressAsync(VideoProgressEvent progressEvent);
}
