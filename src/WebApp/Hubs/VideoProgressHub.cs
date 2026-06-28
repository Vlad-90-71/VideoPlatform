using Microsoft.AspNetCore.SignalR;

namespace WebApp.Hubs;

public class VideoProgressHub : Hub
{
    public async Task JoinVideoGroup(Guid videoId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"video_{videoId}");
    }

    public async Task LeaveVideoGroup(Guid videoId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video_{videoId}");
    }
}
