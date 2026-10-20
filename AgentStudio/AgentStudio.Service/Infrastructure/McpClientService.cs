using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Net.Http.Headers;
using AgentStudio.Service.Application;

namespace AgentStudio.Service.Infrastructure;

public class McpClientService : IMcpClientService
{
    private readonly ILogger<McpClientService> logger;

    public McpClientService(ILogger<McpClientService> logger)
    {
        this.logger = logger;
    }

    public async Task<IList<AITool>> GetToolsAsync(
        string serverUrl,
        string? authToken,
        List<string>? enabledTools = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<AITool>();

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return result;
        }

        try
        {
            var httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            var transportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(serverUrl)
            };

            var transport = new HttpClientTransport(transportOptions, httpClient: httpClient);
            var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);

            var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            foreach (var tool in tools)
            {
                if (enabledTools == null || enabledTools.Count == 0 || enabledTools.Contains(tool.Name))
                {
                    result.Add(tool);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to connect to MCP server at {Url} or list tools", serverUrl);
        }

        return result;
    }
}
