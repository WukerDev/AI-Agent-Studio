using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using AgentStudio.Service.Model;
using AgentStudio.Service.Infrastructure;

namespace AgentStudio.Service.Application;

public class AgentEngine : IAgentEngine
{
    private readonly ApplicationDbContext dbContext;
    private readonly IMcpClientService mcpClientService;
    private readonly IHubContext<EventsHub> hubContext;
    private readonly IConfiguration configuration;
    private readonly ILogger<AgentEngine> logger;

    public AgentEngine(
        ApplicationDbContext dbContext,
        IMcpClientService mcpClientService,
        IHubContext<EventsHub> hubContext,
        IConfiguration configuration,
        ILogger<AgentEngine> logger)
    {
        this.dbContext = dbContext;
        this.mcpClientService = mcpClientService;
        this.hubContext = hubContext;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<WorkflowExecutionResult> ExecuteFlowAsync(
        string flowId,
        string userId,
        string? inputMessage = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();
        var execution = new WorkflowExecutionEntity
        {
            Id = executionId,
            FlowId = flowId,
            OwnerId = userId,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        this.dbContext.WorkflowExecutions.Add(execution);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        var logs = new List<object>();

        async Task LogEvent(string level, string message, object? data = null)
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                level,
                message,
                data
            };
            logs.Add(logEntry);

            await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionProgress", new
            {
                ExecutionId = executionId,
                FlowId = flowId,
                Log = logEntry
            }, cancellationToken);
        }

        try
        {
            await LogEvent("info", $"Starting workflow execution {executionId} for flow {flowId}");

            var nodes = await this.dbContext.AgentNodes
                .Where(n => n.FlowId == flowId && n.OwnerId == userId)
                .ToListAsync(cancellationToken);

            if (nodes.Count == 0)
            {
                await LogEvent("warning", "No nodes found for flow, executing with default agent setup.");
            }

            var agentNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Agent).ToList();
            var modelNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Model).ToList();
            var promptNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Prompt).ToList();
            var mcpNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.McpConnector).ToList();

            var primaryAgent = agentNodes.FirstOrDefault();
            var modelNode = modelNodes.FirstOrDefault();
            var promptNode = promptNodes.FirstOrDefault();
            var mcpNode = mcpNodes.FirstOrDefault();

            var systemPrompt = promptNode?.SystemPrompt;
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                systemPrompt = primaryAgent?.SystemPrompt ?? "You are an autonomous AI Agent built in AI Agent Studio.";
            }

            var provider = modelNode?.ModelProvider ?? primaryAgent?.ModelProvider ?? "OpenAI";
            var modelName = modelNode?.ModelName ?? primaryAgent?.ModelName ?? "gpt-4o";
            var endpoint = modelNode?.Endpoint ?? primaryAgent?.Endpoint;

            await LogEvent("info", $"Resolved configuration: Provider={provider}, Model={modelName}, Agent={primaryAgent?.Name ?? "DefaultAgent"}");

            var tools = new List<AITool>();
            var mcpUrl = mcpNode?.McpServerUrl ?? primaryAgent?.McpServerUrl;
            var mcpToken = mcpNode?.McpAuthToken ?? primaryAgent?.McpAuthToken;
            var mcpTools = mcpNode?.McpEnabledTools ?? primaryAgent?.McpEnabledTools;

            if (!string.IsNullOrWhiteSpace(mcpUrl))
            {
                await LogEvent("info", $"Connecting to MCP Server at {mcpUrl}...");
                var discoveredTools = await this.mcpClientService.GetToolsAsync(mcpUrl, mcpToken, mcpTools, cancellationToken);
                tools.AddRange(discoveredTools);
                await LogEvent("info", $"Successfully attached {discoveredTools.Count} MCP tools to agent.");
            }

            IChatClient chatClient = CreateChatClient(provider, modelName, endpoint);

            var agent = chatClient.AsAIAgent(instructions: systemPrompt, tools: tools, name: primaryAgent?.Name ?? "AgentStudioAgent");

            var promptToRun = string.IsNullOrWhiteSpace(inputMessage)
                ? "Initiate agent workflow and report status."
                : inputMessage;

            await LogEvent("info", "Executing agent reasoning loop...", new { Prompt = promptToRun });

            var response = await agent.RunAsync(promptToRun, cancellationToken: cancellationToken);
            var responseText = response.Text ?? string.Empty;

            await LogEvent("info", "Workflow completed successfully.", new { Output = responseText });

            execution.Status = "Completed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ResultMessage = responseText;
            execution.LogsJson = JsonSerializer.Serialize(logs);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionFinished", new
            {
                ExecutionId = executionId,
                Success = true,
                Output = responseText
            }, cancellationToken);

            return new WorkflowExecutionResult
            {
                Success = true,
                ExecutionId = executionId,
                Output = responseText
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Workflow execution {ExecutionId} failed", executionId);
            await LogEvent("error", $"Execution failed: {ex.Message}");

            execution.Status = "Failed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ResultMessage = ex.Message;
            execution.LogsJson = JsonSerializer.Serialize(logs);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionFinished", new
            {
                ExecutionId = executionId,
                Success = false,
                Error = ex.Message
            }, cancellationToken);

            return new WorkflowExecutionResult
            {
                Success = false,
                ExecutionId = executionId,
                ErrorMessage = ex.Message
            };
        }
    }

    private IChatClient CreateChatClient(string provider, string modelName, string? customEndpoint)
    {
        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            var ollamaEndpoint = !string.IsNullOrWhiteSpace(customEndpoint)
                ? customEndpoint
                : this.configuration["AiProviders:Ollama:Endpoint"] ?? "http://localhost:11434";

            var baseUri = new Uri(ollamaEndpoint.TrimEnd('/') + "/v1");
            var openAiOptions = new OpenAIClientOptions { Endpoint = baseUri };
            var openAiClient = new OpenAIClient(new ApiKeyCredential("ollama"), openAiOptions);
            return openAiClient.GetChatClient(modelName).AsIChatClient();
        }
        else
        {
            var apiKey = this.configuration["AiProviders:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "placeholder-key";
            var defaultEndpoint = this.configuration["AiProviders:OpenAI:Endpoint"];

            OpenAIClientOptions? openAiOptions = null;
            if (!string.IsNullOrWhiteSpace(customEndpoint))
            {
                openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(customEndpoint) };
            }
            else if (!string.IsNullOrWhiteSpace(defaultEndpoint) && Uri.TryCreate(defaultEndpoint, UriKind.Absolute, out var uri))
            {
                openAiOptions = new OpenAIClientOptions { Endpoint = uri };
            }

            var openAiClient = openAiOptions != null
                ? new OpenAIClient(new ApiKeyCredential(apiKey), openAiOptions)
                : new OpenAIClient(new ApiKeyCredential(apiKey));

            return openAiClient.GetChatClient(modelName).AsIChatClient();
        }
    }
}
