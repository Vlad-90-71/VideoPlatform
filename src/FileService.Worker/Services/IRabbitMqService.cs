using Shared.Messaging;

namespace FileService.Worker.Services;

public interface IRabbitMqService
{
    Task ConsumeProcessVideoQueueAsync(Func<ProcessVideoCommand, Task> handler);
    Task PublishVideoProgressAsync(VideoProgressEvent progressEvent);
    Task ConsumeVideoProgressEvents(Func<VideoProgressEvent, Task> handler, CancellationToken cancellationToken = default);  // ← Добавьте это
}