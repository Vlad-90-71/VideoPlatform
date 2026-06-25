using Shared.Messaging;

namespace FileService.Services;

public interface IRabbitMqService
{
    Task PublishProcessVideoCommandAsync(ProcessVideoCommand command);
}