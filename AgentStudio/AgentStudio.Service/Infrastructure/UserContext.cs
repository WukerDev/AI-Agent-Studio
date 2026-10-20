using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using AgentStudio.Service.Application;

namespace AgentStudio.Service.Infrastructure;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public string UserId => this.httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? this.httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? string.Empty;

    public string Username => this.httpContextAccessor.HttpContext?.User.FindFirst("preferred_username")?.Value
        ?? this.httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
        ?? string.Empty;
}
