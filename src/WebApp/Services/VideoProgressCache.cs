using System.Collections.Concurrent;
using Shared.Messaging;

namespace WebApp.Services;

public interface IVideoProgressCache
{
    void UpdateProgress(VideoProgressEvent progressEvent);
    VideoProgressEvent? GetProgress(Guid videoId);
}

public class VideoProgressCache : IVideoProgressCache
{
    private readonly ConcurrentDictionary<Guid, VideoProgressEvent> _progress = new();
    private readonly ILogger<VideoProgressCache> _logger;

    public VideoProgressCache(ILogger<VideoProgressCache> logger)
    {
        _logger = logger;
    }

    public void UpdateProgress(VideoProgressEvent progressEvent)
    {
        _progress.AddOrUpdate(
            progressEvent.VideoId,
            progressEvent,
            (_, _) => progressEvent);

        _logger.LogDebug("Updated progress for video {VideoId}: {Progress}%",
            progressEvent.VideoId, progressEvent.ProgressPercentage);
    }

    public VideoProgressEvent? GetProgress(Guid videoId)
    {
        return _progress.TryGetValue(videoId, out var progress) ? progress : null;
    }
}