using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace AgentStudio.Service.Hubs;

[Authorize]
public class EventsHub : Hub
{
    public override async Task OnConnectedAsync()
    {   
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}