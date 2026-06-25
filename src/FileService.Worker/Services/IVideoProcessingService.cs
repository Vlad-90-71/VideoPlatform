using Shared.Messaging;

namespace FileService.Worker.Services;

public interface IVideoProcessingService
{
    Task ProcessVideoAsync(ProcessVideoCommand command);
}
