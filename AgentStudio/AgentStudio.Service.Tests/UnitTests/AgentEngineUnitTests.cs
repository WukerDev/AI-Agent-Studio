using AgentStudio.Service.Protos;
using AgentStudio.Service.Application;
using AgentStudio.Service.Infrastructure;
using AgentStudio.Service.Model;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentStudio.Service.Tests.UnitTests;

public class AgentEngineUnitTests
{
    private class FakeMcpClientService : IMcpClientService
    {
        public string? LastUrl { get; private set; }
        public string? LastToken { get; private set; }
        public List<string>? LastTools { get; private set; }

        public Task<IList<AITool>> GetToolsAsync(string serverUrl, string? authToken = null, List<string>? enabledTools = null, CancellationToken cancellationToken = default)
        {
            LastUrl = serverUrl;
            LastToken = authToken;
            LastTools = enabledTools;
            IList<AITool> emptyList = new List<AITool>();
            return Task.FromResult(emptyList);
        }
    }

    private class FakeHubContext : IHubContext<EventsHub>
    {
        public IHubClients Clients => new FakeHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private class FakeHubClients : IHubClients
    {
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy All => new FakeClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => new FakeClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    private class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldResolveModelAndPromptAndInvokeMcp()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-ai-flow";
        var userId = "user-123";

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "agent-node-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "Research Analyst",
            Type = (int)NodeType.Agent
        });

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "model-node-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "Local Ollama LLM",
            Type = (int)NodeType.Model,
            ModelProvider = "Ollama",
            ModelName = "llama3.2",
            Endpoint = "http://localhost:11434"
        });

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "prompt-node-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "System Persona",
            Type = (int)NodeType.Prompt,
            SystemPrompt = "You are an autonomous AI research assistant."
        });

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "mcp-node-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "Web Tools MCP",
            Type = (int)NodeType.McpConnector,
            McpServerUrl = "http://localhost:8000/mcp",
            McpAuthToken = "secret-token",
            McpEnabledTools = new List<string> { "search_web", "read_file" }
        });

        await db.SaveChangesAsync();

        var fakeMcp = new FakeMcpClientService();
        var fakeHub = new FakeHubContext();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:OpenAI:ApiKey"] = "test-key"
            })
            .Build();

        var engine = new AgentEngine(
            db,
            fakeMcp,
            fakeHub,
            config,
            NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(flowId, userId, "Analyze the latest AI research papers");

        // Assert
        fakeMcp.LastUrl.Should().Be("http://localhost:8000/mcp");
        fakeMcp.LastToken.Should().Be("secret-token");
        fakeMcp.LastTools.Should().Contain("search_web");

        var execution = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.FlowId == flowId);
        execution.Should().NotBeNull();
        execution!.OwnerId.Should().Be(userId);
    }
}
