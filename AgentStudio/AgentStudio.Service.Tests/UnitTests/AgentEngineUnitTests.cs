using AgentStudio.Service.Protos;
using AgentStudio.Service.Application;
using AgentStudio.Service.Infrastructure;
using AgentStudio.Service.Model;
using AgentStudio.Service.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AgentStudio.Service.Tests.UnitTests;

public class AgentEngineUnitTests
{
    private readonly ITestOutputHelper output;

    public AgentEngineUnitTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private void WarnOllamaUnavailable(string testName)
    {
        var msg = $"[WARN] {testName}: Ollama (gemma3:12b) nie jest uruchomione pod adresem http://localhost:11434. Pominięto asercję wnioskowania LLM w środowisku CI/CD.";
        this.output.WriteLine($"##[warning]{msg}");
        this.output.WriteLine(msg);
        Console.WriteLine(msg);
    }
    private class FakeMcpClientService : IMcpClientService
    {
        public string? LastUrl { get; private set; }
        public string? LastToken { get; private set; }
        public List<string>? LastTools { get; private set; }
        public IList<AITool> ToolsToReturn { get; set; } = new List<AITool>();

        public Task<IList<AITool>> GetToolsAsync(string serverUrl, string? authToken = null, List<string>? enabledTools = null, CancellationToken cancellationToken = default)
        {
            LastUrl = serverUrl;
            LastToken = authToken;
            LastTools = enabledTools;
            return Task.FromResult(ToolsToReturn);
        }
    }

    private class FakeHubContext : IHubContext<EventsHub>
    {
        public FakeClientProxy ClientProxy { get; } = new();
        public IHubClients Clients => new FakeHubClients(ClientProxy);
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private class FakeHubClients(FakeClientProxy proxy) : IHubClients
    {
        public IClientProxy User(string userId) => proxy;
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private class FakeClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> SentMessages { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((method, args));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldResolveModelAndPromptAndInvokeMcp_WithGemma3_12b()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_ShouldResolveModelAndPromptAndInvokeMcp_WithGemma3_12b));
            return;
        }

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
            Name = "Local Ollama Gemma3 12B",
            Type = (int)NodeType.Model,
            ModelProvider = "Ollama",
            ModelName = "gemma3:12b",
            Temperature = 0.2f,
            Endpoint = "http://localhost:11434"
        });

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "prompt-node-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "System Persona",
            Type = (int)NodeType.Prompt,
            SystemPrompt = "You are a concise AI assistant. Reply in one short sentence."
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
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(
            db,
            fakeMcp,
            fakeHub,
            config,
            NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(flowId, userId, "What is 2 + 2? Answer briefly.", executionId: "exec-custom-id-1");

        // Assert
        fakeMcp.LastUrl.Should().Be("http://localhost:8000/mcp");
        fakeMcp.LastToken.Should().Be("secret-token");
        fakeMcp.LastTools.Should().Contain("search_web");

        result.ExecutionId.Should().Be("exec-custom-id-1");
        result.Success.Should().BeTrue(because: $"Ollama gemma3:12b is running locally (ErrorMessage: {result.ErrorMessage})");
        result.Output.Should().NotBeNullOrWhiteSpace();
        result.Output.Should().Contain("4");

        var execution = await db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == "exec-custom-id-1");
        execution.Should().NotBeNull();
        execution!.OwnerId.Should().Be(userId);
        execution.Status.Should().Be("Completed");
        execution.ResultMessage.Should().Contain("4");
    }

    [Fact]
    public async Task ExecuteFlowAsync_WhenModelDoesNotSupportNativeTools_ShouldFallbackGracefullyWithGemma3_12b()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_WhenModelDoesNotSupportNativeTools_ShouldFallbackGracefullyWithGemma3_12b));
            return;
        }

        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineToolFallbackTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-tool-fallback-flow";
        var userId = "user-123";

        db.AgentNodes.Add(new AgentNodeEntity
        {
            Id = "agent-1",
            FlowId = flowId,
            OwnerId = userId,
            Name = "Tool-Equipped Agent",
            Type = (int)NodeType.Agent,
            ModelProvider = "Ollama",
            ModelName = "gemma3:12b",
            Endpoint = "http://localhost:11434",
            McpServerUrl = "http://localhost:8000/mcp",
            McpEnabledTools = new List<string> { "search_web" }
        });

        await db.SaveChangesAsync();

        var fakeMcp = new FakeMcpClientService
        {
            ToolsToReturn = new List<AITool>
            {
                AIFunctionFactory.Create((string query) => $"Result for {query}", "search_web", "Searches the web for information")
            }
        };
        var fakeHub = new FakeHubContext();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(
            db,
            fakeMcp,
            fakeHub,
            config,
            NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(flowId, userId, "Say hello in Polish in maximum 3 words.");

        // Assert
        result.Success.Should().BeTrue(because: $"AgentEngine should fallback to prompt-augmented mode when gemma3:12b rejects native tool definitions (Error: {result.ErrorMessage})");
        result.Output.Should().NotBeNullOrWhiteSpace();
        fakeHub.ClientProxy.SentMessages.Should().Contain(m => m.Method == "WorkflowExecutionFinished");
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldChainMultiAgentWorkflowInTopologicalOrder_AndSubstitutePromptVariables()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_ShouldChainMultiAgentWorkflowInTopologicalOrder_AndSubstitutePromptVariables));
            return;
        }

        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineChainTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-multi-agent-chain";
        var userId = "user-123";

        db.AgentNodes.AddRange(
            new AgentNodeEntity
            {
                Id = "model-gemma",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Gemma3 12B",
                Type = (int)NodeType.Model,
                ModelProvider = "Ollama",
                ModelName = "gemma3:12b",
                Temperature = 0.1f,
                Endpoint = "http://localhost:11434"
            },
            new AgentNodeEntity
            {
                Id = "agent-1",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Agent Pierwszy",
                Type = (int)NodeType.Agent,
                SystemPrompt = "Odpowiedz wyłącznie jednym słowem (stolica Polski) na temat: {{input}}"
            },
            new AgentNodeEntity
            {
                Id = "agent-2",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Agent Drugi",
                Type = (int)NodeType.Agent,
                SystemPrompt = "Otrzymałeś dane wejściowe od poprzedniego agenta: {{input}}. Napisz jedno krótkie zdanie zawierające to miasto."
            });

        db.WorkflowEdges.AddRange(
            new WorkflowEdgeEntity
            {
                Id = "edge-model-1",
                FlowId = flowId,
                OwnerId = userId,
                SourceNodeId = "model-gemma",
                TargetNodeId = "agent-1"
            },
            new WorkflowEdgeEntity
            {
                Id = "edge-model-2",
                FlowId = flowId,
                OwnerId = userId,
                SourceNodeId = "model-gemma",
                TargetNodeId = "agent-2"
            },
            new WorkflowEdgeEntity
            {
                Id = "edge-chain-1-2",
                FlowId = flowId,
                OwnerId = userId,
                SourceNodeId = "agent-1",
                TargetNodeId = "agent-2"
            });

        await db.SaveChangesAsync();

        var fakeMcp = new FakeMcpClientService();
        var fakeHub = new FakeHubContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(db, fakeMcp, fakeHub, config, NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(flowId, userId, "Jaka jest stolica Polski?");

        // Assert
        result.Success.Should().BeTrue(because: $"Error: {result.ErrorMessage}");
        result.Output.Should().Contain("[Agent Pierwszy]");
        result.Output.Should().Contain("[Agent Drugi]");
        result.Output.ToLowerInvariant().Should().Contain("warszaw");
    }

    [Fact]
    public async Task ExecuteFlowAsync_WithBuiltInMcpCalculatorTool_ShouldInvokeToolInReActLoop_WithGemma3_12b()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_WithBuiltInMcpCalculatorTool_ShouldInvokeToolInReActLoop_WithGemma3_12b));
            return;
        }

        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineReActMcpTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-react-mcp-calc";
        var userId = "user-react";

        db.AgentNodes.AddRange(
            new AgentNodeEntity
            {
                Id = "model-gemma",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Ollama Gemma3 12B",
                Type = (int)NodeType.Model,
                ModelProvider = "Ollama",
                ModelName = "gemma3:12b",
                Temperature = 0.0f,
                Endpoint = "http://localhost:11434"
            },
            new AgentNodeEntity
            {
                Id = "prompt-sys",
                FlowId = flowId,
                OwnerId = userId,
                Name = "System Prompt Matematyczny",
                Type = (int)NodeType.Prompt,
                SystemPrompt = "Zawsze używaj narzędzia MCP 'calculate_expression' do obliczeń matematycznych dla zapytania: {{input}}"
            },
            new AgentNodeEntity
            {
                Id = "mcp-builtin",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Built-in MCP Tools",
                Type = (int)NodeType.McpConnector,
                McpServerUrl = "builtin://tools",
                McpEnabledTools = new List<string> { "calculate_expression", "get_current_time" }
            },
            new AgentNodeEntity
            {
                Id = "agent-calc",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Agent Matematyczny",
                Type = (int)NodeType.Agent,
                SystemPrompt = "Wywołaj narzędzie calculate_expression i podaj dokładny wynik."
            });

        db.WorkflowEdges.AddRange(
            new WorkflowEdgeEntity { Id = "e1", FlowId = flowId, OwnerId = userId, SourceNodeId = "model-gemma", TargetNodeId = "agent-calc" },
            new WorkflowEdgeEntity { Id = "e2", FlowId = flowId, OwnerId = userId, SourceNodeId = "prompt-sys", TargetNodeId = "agent-calc" },
            new WorkflowEdgeEntity { Id = "e3", FlowId = flowId, OwnerId = userId, SourceNodeId = "mcp-builtin", TargetNodeId = "agent-calc" });

        await db.SaveChangesAsync();

        var realMcpService = new McpClientService(NullLogger<McpClientService>.Instance);
        var fakeHub = new FakeHubContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(db, realMcpService, fakeHub, config, NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(
            flowId,
            userId,
            "Użyj narzędzia calculate_expression aby obliczyć (145 * 28) + 350 i podaj wynik liczbowy.");

        // Assert
        var execLog = (await db.WorkflowExecutions.FirstOrDefaultAsync())?.LogsJson;
        result.Success.Should().BeTrue(because: $"Error: {result.ErrorMessage}");
        var normalizedOutput = result.Output.Replace(" ", "").Replace(",", "");
        normalizedOutput.Should().Contain("4410", because: $"Logs: {execLog}");
    }

    [Fact]
    public async Task ExecuteFlowAsync_WithBuiltInMcpSystemInfoTool_ShouldInvokeGetSystemInfo_WithGemma3_12b()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_WithBuiltInMcpSystemInfoTool_ShouldInvokeGetSystemInfo_WithGemma3_12b));
            return;
        }

        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineReActSysInfoTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-react-mcp-sysinfo";
        var userId = "user-react-sysinfo";

        db.AgentNodes.AddRange(
            new AgentNodeEntity
            {
                Id = "model-gemma",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Ollama Gemma3 12B",
                Type = (int)NodeType.Model,
                ModelProvider = "Ollama",
                ModelName = "gemma3:12b",
                Temperature = 0.0f,
                Endpoint = "http://localhost:11434"
            },
            new AgentNodeEntity
            {
                Id = "prompt-sys",
                FlowId = flowId,
                OwnerId = userId,
                Name = "System Prompt Diagnostyczny",
                Type = (int)NodeType.Prompt,
                SystemPrompt = "Zawsze używaj narzędzia MCP 'get_system_info' do sprawdzenia parametrów systemu i środowiska .NET dla zapytania: {{input}}"
            },
            new AgentNodeEntity
            {
                Id = "mcp-builtin",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Built-in MCP Tools",
                Type = (int)NodeType.McpConnector,
                McpServerUrl = "builtin://tools",
                McpEnabledTools = new List<string> { "get_system_info", "get_current_time" }
            },
            new AgentNodeEntity
            {
                Id = "agent-sysinfo",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Agent Diagnostyczny",
                Type = (int)NodeType.Agent
            });

        db.WorkflowEdges.AddRange(
            new WorkflowEdgeEntity { Id = "e1", FlowId = flowId, OwnerId = userId, SourceNodeId = "model-gemma", TargetNodeId = "agent-sysinfo", SourceHandle = "model-out", TargetHandle = "model" },
            new WorkflowEdgeEntity { Id = "e2", FlowId = flowId, OwnerId = userId, SourceNodeId = "prompt-sys", TargetNodeId = "agent-sysinfo", SourceHandle = "prompt-out", TargetHandle = "prompt" },
            new WorkflowEdgeEntity { Id = "e3", FlowId = flowId, OwnerId = userId, SourceNodeId = "mcp-builtin", TargetNodeId = "agent-sysinfo", SourceHandle = "tool-out", TargetHandle = "tool" });

        await db.SaveChangesAsync();

        var realMcpService = new McpClientService(NullLogger<McpClientService>.Instance);
        var fakeHub = new FakeHubContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(db, realMcpService, fakeHub, config, NullLogger<AgentEngine>.Instance);

        // Act
        var result = await engine.ExecuteFlowAsync(
            flowId,
            userId,
            "Sprawdź informacje o systemie operacyjnym, wersję .NET oraz liczbę rdzeni procesora.");

        // Assert
        result.Success.Should().BeTrue(because: $"Error: {result.ErrorMessage}");
        result.Output.Should().ContainAny(".NET", "Windows", "Linux", Environment.ProcessorCount.ToString());
    }

    [Fact]
    public async Task ExecuteFlowAsync_WithBuiltInMcpSearchWebTool_ShouldRetrieveLiveEncyclopediaResult_WithGemma3_12b()
    {
        if (!await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            WarnOllamaUnavailable(nameof(ExecuteFlowAsync_WithBuiltInMcpSearchWebTool_ShouldRetrieveLiveEncyclopediaResult_WithGemma3_12b));
            return;
        }

        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineReActSearchWebTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var flowId = "test-react-mcp-search-web";
        var userId = "user-react-search";

        db.AgentNodes.AddRange(
            new AgentNodeEntity
            {
                Id = "model-gemma",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Ollama Gemma3 12B",
                Type = (int)NodeType.Model,
                ModelProvider = "Ollama",
                ModelName = "gemma3:12b",
                Temperature = 0.2f,
                Endpoint = "http://localhost:11434"
            },
            new AgentNodeEntity
            {
                Id = "prompt-sys",
                FlowId = flowId,
                OwnerId = userId,
                Name = "System Prompt (Polski Asystent)",
                Type = (int)NodeType.Prompt,
                SystemPrompt = "Jesteś pomocnym polskim asystentem AI. Zadanie użytkownika: {{input}}"
            },
            new AgentNodeEntity
            {
                Id = "mcp-search",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Web Search Tools MCP",
                Type = (int)NodeType.McpConnector,
                McpServerUrl = "builtin://tools",
                McpEnabledTools = new List<string> { "search_web", "fetch_page" }
            },
            new AgentNodeEntity
            {
                Id = "agent-main",
                FlowId = flowId,
                OwnerId = userId,
                Name = "Główny Agent AI",
                Type = (int)NodeType.Agent
            });

        db.WorkflowEdges.AddRange(
            new WorkflowEdgeEntity { Id = "e1", FlowId = flowId, OwnerId = userId, SourceNodeId = "model-gemma", TargetNodeId = "agent-main", SourceHandle = "model-out", TargetHandle = "model" },
            new WorkflowEdgeEntity { Id = "e2", FlowId = flowId, OwnerId = userId, SourceNodeId = "prompt-sys", TargetNodeId = "agent-main", SourceHandle = "prompt-out", TargetHandle = "prompt" },
            new WorkflowEdgeEntity { Id = "e3", FlowId = flowId, OwnerId = userId, SourceNodeId = "mcp-search", TargetNodeId = "agent-main", SourceHandle = "tool-out", TargetHandle = "tool" });

        await db.SaveChangesAsync();

        var realMcpService = new McpClientService(NullLogger<McpClientService>.Instance);
        var fakeHub = new FakeHubContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:DefaultProvider"] = "Ollama",
                ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
            })
            .Build();

        var engine = new AgentEngine(db, realMcpService, fakeHub, config, NullLogger<AgentEngine>.Instance);

        // Act 1: General encyclopedia query ("co to jest wodór")
        var result = await engine.ExecuteFlowAsync(
            flowId,
            userId,
            "co to jest wodór");

        // Assert 1
        result.Success.Should().BeTrue(because: $"Error: {result.ErrorMessage}");
        result.Output.ToLowerInvariant().Should().ContainAny("pierwiastek", "wodór", "chemiczny", "atomow");
        result.Output.ToLowerInvariant().Should().NotContain("nie zawierają definicji wodoru");

        // Act 2: Live web + GitHub releases query ("jaka jest najnowsza wersja GTNH")
        var gtnhResult = await engine.ExecuteFlowAsync(
            flowId,
            userId,
            "jaka jest najnowsza wersja GTNH");

        // Assert 2
        gtnhResult.Success.Should().BeTrue(because: $"Error: {gtnhResult.ErrorMessage}");
        gtnhResult.Output.Should().ContainAny("2.9", "2.10", "GTNH", "New Horizons");
        gtnhResult.Output.ToLowerInvariant().Should().NotContain("nie mogę podać");
    }

    [Fact]
    public async Task CreateChatClient_ForGeminiProvider_ShouldConfigureClient_AndRequireApiKeyWhenMissing()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentEngineGeminiTest_{Guid.NewGuid()}")
            .Options;

        using var db = new ApplicationDbContext(options);
        var fakeMcp = new FakeMcpClientService();
        var fakeHub = new FakeHubContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:Gemini:Endpoint"] = "https://generativelanguage.googleapis.com/v1beta/openai/",
                ["AiProviders:Gemini:DefaultModel"] = "gemini-3.5-flash-lite"
            })
            .Build();

        var engine = new AgentEngine(db, fakeMcp, fakeHub, config, NullLogger<AgentEngine>.Instance);

        // Act & Assert 1: Creating client with explicit API key override succeeds
        var clientWithKey = engine.CreateChatClient("Gemini", "gemini-3.5-flash-lite", null, 0.5, "test-gemini-dummy-key");
        clientWithKey.Should().NotBeNull();

        // Act & Assert 2: Missing API key returns clear Polish diagnostic message
        var previousEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var previousGoogleEnv = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);

            Action actWithoutKey = () => engine.CreateChatClient("Gemini", "gemini-3.5-flash-lite", null, 0.5, null);
            actWithoutKey.Should().Throw<InvalidOperationException>()
                .WithMessage("*Google Gemini*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", previousEnv);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", previousGoogleEnv);
        }
    }

    [Fact]
    public async Task WorkflowSeeder_ShouldSeedSampleWorkflowsIntoPostgreSqlAndInMemory()
    {
        // 1. Verify InMemory seeding
        var memOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeederTest_{Guid.NewGuid()}")
            .Options;

        await using (var memDb = new ApplicationDbContext(memOptions))
        {
            await WorkflowSeeder.SeedSampleWorkflowsAsync(memDb, "guest-user");
            var flows = await memDb.Workflows.ToListAsync();
            flows.Should().HaveCount(5);
            var nodes = await memDb.AgentNodes.ToListAsync();
            nodes.Should().HaveCountGreaterThanOrEqualTo(25);
            var edges = await memDb.WorkflowEdges.ToListAsync();
            edges.Should().Contain(e => e.TargetHandle == "model");
            edges.Should().Contain(e => e.TargetHandle == "prompt");
            edges.Should().Contain(e => e.TargetHandle == "tool");
        }

        // 2. Also seed/refresh live local PostgreSQL container if running on localhost:5432
        try
        {
            var pgOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=agents;Username=keycloak;Password=keycloak_password")
                .Options;

            await using var pgDb = new ApplicationDbContext(pgOptions);
            if (await pgDb.Database.CanConnectAsync())
            {
                var sampleFlowIds = new[]
                {
                    "default-flow",
                    "multi-agent-research",
                    "gemini-assistant-flow",
                    "code-review-flow",
                    "mcp-calculator-time-flow"
                };

                var oldEdges = await pgDb.WorkflowEdges.Where(e => sampleFlowIds.Contains(e.FlowId)).ToListAsync();
                var oldNodes = await pgDb.AgentNodes.Where(n => sampleFlowIds.Contains(n.FlowId)).ToListAsync();
                pgDb.WorkflowEdges.RemoveRange(oldEdges);
                pgDb.AgentNodes.RemoveRange(oldNodes);
                await pgDb.SaveChangesAsync();

                await WorkflowSeeder.SeedSampleWorkflowsAsync(pgDb, "a95b7db9-a030-438a-bd4c-b46747467d54");
            }
        }
        catch
        {
            // Ignore if local PostgreSQL container is not running in CI environment
        }
    }

    [Fact]
    public async Task ExecuteFlowAsync_WithConnectedSkillNode_ShouldLoadSkillMdViaMicrosoftAgentSkillsConvention()
    {
        // Arrange
        var tempSkillsRoot = Path.Combine(Path.GetTempPath(), $"AgentStudio_SkillsTest_{Guid.NewGuid():N}");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Skills:RootPath"] = tempSkillsRoot,
                    ["AiProviders:DefaultProvider"] = "Ollama",
                    ["AiProviders:Ollama:Endpoint"] = "http://localhost:11434",
                    ["AiProviders:Ollama:DefaultModel"] = "gemma3:12b"
                })
                .Build();

            var skillService = new SkillService(config, NullLogger<SkillService>.Instance);
            var savedSkill = await skillService.SaveSkillAsync(
                "secret-codeword-skill",
                "Use this skill whenever asked for the secret codeword.",
                "# Secret Codeword Instructions\nAlways include the exact word CYBER-FALCON-99 in your response.");

            savedSkill.Name.Should().Be("secret-codeword-skill");
            File.Exists(Path.Combine(tempSkillsRoot, "secret-codeword-skill", "SKILL.md")).Should().BeTrue();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"AgentEngineSkillTest_{Guid.NewGuid()}")
                .Options;

            using var db = new ApplicationDbContext(options);
            var flowId = "test-skill-flow";
            var userId = "user-skill-1";

            db.AgentNodes.AddRange(
                new AgentNodeEntity
                {
                    Id = "model-1",
                    FlowId = flowId,
                    OwnerId = userId,
                    Name = "Gemma 3",
                    Type = (int)NodeType.Model,
                    ModelProvider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.1f,
                    Endpoint = "http://localhost:11434"
                },
                new AgentNodeEntity
                {
                    Id = "skill-1",
                    FlowId = flowId,
                    OwnerId = userId,
                    Name = "Skill: secret-codeword-skill",
                    Type = (int)NodeType.Skill,
                    Endpoint = "secret-codeword-skill",
                    SystemPrompt = savedSkill.Description
                },
                new AgentNodeEntity
                {
                    Id = "agent-1",
                    FlowId = flowId,
                    OwnerId = userId,
                    Name = "Skilled Agent",
                    Type = (int)NodeType.Agent
                });

            db.WorkflowEdges.AddRange(
                new WorkflowEdgeEntity
                {
                    Id = "e_model_agent",
                    FlowId = flowId,
                    OwnerId = userId,
                    SourceNodeId = "model-1",
                    SourceHandle = "model-out",
                    TargetNodeId = "agent-1",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "e_skill_agent",
                    FlowId = flowId,
                    OwnerId = userId,
                    SourceNodeId = "skill-1",
                    SourceHandle = "skill-out",
                    TargetNodeId = "agent-1",
                    TargetHandle = "skill"
                });

            await db.SaveChangesAsync();

            var fakeMcp = new FakeMcpClientService();
            var fakeHub = new FakeHubContext();
            var engine = new AgentEngine(db, fakeMcp, fakeHub, config, NullLogger<AgentEngine>.Instance, skillService);

            // Act
            var result = await engine.ExecuteFlowAsync(flowId, userId, "What is the secret codeword? Reply in one short sentence.");

            // Assert: Verify SkillService created SKILL.md and AgentEngine resolved & loaded the connected Skill node via Microsoft.Agents.AI AgentSkillsProvider
            var loadedSkillDetails = await skillService.GetSkillByNameAsync("secret-codeword-skill");
            loadedSkillDetails.Should().NotBeNull();
            loadedSkillDetails!.Content.Should().Contain("CYBER-FALCON-99");

            fakeHub.ClientProxy.SentMessages.Should().Contain(m =>
                m.Method == "WorkflowExecutionProgress" &&
                m.Args.Any(a => a != null && a.ToString()!.Contains("Attached Microsoft Agent Skill(s) via SKILL.md")) &&
                m.Args.Any(a => a != null && a.ToString()!.Contains("skills/secret-codeword-skill/SKILL.md")));

            if (result.Success)
            {
                result.Output.Should().Contain("CYBER-FALCON-99");
            }
        }
        finally
        {
            if (Directory.Exists(tempSkillsRoot))
            {
                Directory.Delete(tempSkillsRoot, recursive: true);
            }
        }
    }
}
