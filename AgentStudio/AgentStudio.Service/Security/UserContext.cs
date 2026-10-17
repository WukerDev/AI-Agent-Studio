using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AgentStudio.Service.Security;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public string UserId => this.httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    public string Username => this.httpContextAccessor.HttpContext?.User.FindFirst("preferred_username")?.Value ?? string.Empty;
}