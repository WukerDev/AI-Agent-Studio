using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace AgentStudio.Service.Infrastructure;

public class KeycloakUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
