using Microsoft.Extensions.AI;

namespace AgentStudio.Service.Application;

public interface IMcpClientService
{
    Task<IList<AITool>> GetToolsAsync(string serverUrl, string? authToken, List<string>? enabledTools = null, CancellationToken cancellationToken = default);
}
