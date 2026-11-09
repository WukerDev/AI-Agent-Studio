using AgentStudio.Service.Protos;
using AgentStudio.Service.Tests.Infrastructure;
using FluentAssertions;
using Grpc.Core;
using Xunit;

namespace AgentStudio.Service.Tests.IntegrationTests;

public class AgentFlowGrpcApiTests : IClassFixture<AgentStudioTestFactory>
{
    private readonly AgentStudioTestFactory factory;
    private readonly AgentFlowService.AgentFlowServiceClient grpcClient;

    public AgentFlowGrpcApiTests(AgentStudioTestFactory factory)
    {
        this.factory = factory;
        this.grpcClient = factory.CreateGrpcClient();
    }

    [Fact]
    public async Task SaveAgentNode_ShouldPersistNode_AndReturnSuccess()
    {
        // Arrange
        var request = new SaveAgentNodeRequest
        {
            FlowId = "flow-test-1",
            Node = new AgentNode
            {
                Id = "node-" + Guid.NewGuid(),
                Name = "Test LLM Agent",
                Type = NodeType.Agent,
                Position = new Position { X = 150.5, Y = 200.0 },
                SystemPrompt = "You are a specialized test agent.",
                Endpoint = "http://localhost:11434",
                ModelConfig = new ModelConfig
                {
                    Provider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.5f
                },
                McpConfig = new McpConfig
                {
                    ServerUrl = "http://localhost:8000/mcp",
                    AuthToken = "secret-token"
                }
            }
        };
        request.Node.McpConfig.EnabledTools.Add("test_tool");

        // Act
        var response = await this.grpcClient.SaveAgentNodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Id.Should().Be(request.Node.Id);

        var getNodesResponse = await this.grpcClient.GetAgentNodesAsync(new GetAgentNodesRequest
        {
            FlowId = "flow-test-1"
        });

        getNodesResponse.Should().NotBeNull();
        getNodesResponse.Success.Should().BeTrue();
        getNodesResponse.Nodes.Should().ContainSingle(n => n.Id == request.Node.Id);

        var retrieved = getNodesResponse.Nodes.First(n => n.Id == request.Node.Id);
        retrieved.Name.Should().Be("Test LLM Agent");
        retrieved.SystemPrompt.Should().Be("You are a specialized test agent.");
        retrieved.ModelConfig.Provider.Should().Be("Ollama");
        retrieved.ModelConfig.ModelName.Should().Be("gemma3:12b");
        retrieved.McpConfig.ServerUrl.Should().Be("http://localhost:8000/mcp");
        retrieved.McpConfig.EnabledTools.Should().Contain("test_tool");
    }

    [Fact]
    public async Task SaveAgentNode_WithoutOptionalConfigs_And_GetAgentNodes_ShouldNotThrowNullArgument()
    {
        // Arrange
        var flowId = "flow-optional-nulls-" + Guid.NewGuid();
        var nodeId = "node-minimal-" + Guid.NewGuid();

        var saveRequest = new SaveAgentNodeRequest
        {
            FlowId = flowId,
            Node = new AgentNode
            {
                Id = nodeId,
                Name = "Minimal Prompt Node",
                Type = NodeType.Prompt,
                Position = new Position { X = 10, Y = 20 }
            }
        };

        // Act
        var saveResponse = await this.grpcClient.SaveAgentNodeAsync(saveRequest);
        saveResponse.Success.Should().BeTrue();

        var getResponse = await this.grpcClient.GetAgentNodesAsync(new GetAgentNodesRequest
        {
            FlowId = flowId
        });

        // Assert
        getResponse.Success.Should().BeTrue();
        getResponse.Nodes.Should().ContainSingle(n => n.Id == nodeId);
        getResponse.Nodes[0].SystemPrompt.Should().BeEmpty();
        getResponse.Nodes[0].ModelConfig.Provider.Should().BeEmpty();
        getResponse.Nodes[0].McpConfig.ServerUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAgentNode_ShouldRemoveNodeAndConnectedEdgesFromDatabase()
    {
        // Arrange
        var flowId = "flow-delete-" + Guid.NewGuid();
        var node1Id = "node-del-1-" + Guid.NewGuid();
        var node2Id = "node-del-2-" + Guid.NewGuid();
        var edgeId = "edge-del-" + Guid.NewGuid();

        await this.grpcClient.SaveAgentNodeAsync(new SaveAgentNodeRequest
        {
            FlowId = flowId,
            Node = new AgentNode
            {
                Id = node1Id,
                Name = "Node 1",
                Type = NodeType.Model,
                Position = new Position { X = 100, Y = 100 }
            }
        });

        await this.grpcClient.SaveAgentNodeAsync(new SaveAgentNodeRequest
        {
            FlowId = flowId,
            Node = new AgentNode
            {
                Id = node2Id,
                Name = "Node 2",
                Type = NodeType.Agent,
                Position = new Position { X = 300, Y = 100 }
            }
        });

        var saveEdgesRequest = new SaveWorkflowEdgesRequest { FlowId = flowId };
        saveEdgesRequest.Edges.Add(new WorkflowEdge
        {
            Id = edgeId,
            FlowId = flowId,
            SourceNodeId = node1Id,
            TargetNodeId = node2Id
        });
        await this.grpcClient.SaveWorkflowEdgesAsync(saveEdgesRequest);

        // Act
        var deleteResponse = await this.grpcClient.DeleteAgentNodeAsync(new DeleteAgentNodeRequest
        {
            FlowId = flowId,
            Id = node1Id
        });

        // Assert
        deleteResponse.Success.Should().BeTrue();

        var remainingNodes = await this.grpcClient.GetAgentNodesAsync(new GetAgentNodesRequest { FlowId = flowId });
        remainingNodes.Nodes.Should().ContainSingle(n => n.Id == node2Id);
        remainingNodes.Nodes.Should().NotContain(n => n.Id == node1Id);

        var remainingEdges = await this.grpcClient.GetWorkflowEdgesAsync(new GetWorkflowEdgesRequest { FlowId = flowId });
        remainingEdges.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAgentNode_WithEmptyName_ShouldThrowInvalidArgumentRpcException()
    {
        // Arrange
        var invalidRequest = new SaveAgentNodeRequest
        {
            FlowId = "flow-invalid-test",
            Node = new AgentNode
            {
                Id = "node-invalid",
                Name = "",
                Type = NodeType.Agent
            }
        };

        // Act
        Func<Task> act = async () => await this.grpcClient.SaveAgentNodeAsync(invalidRequest);

        // Assert
        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task SaveWorkflowEdges_And_GetWorkflowEdges_ShouldPersistAndReturnEdges()
    {
        // Arrange
        var flowId = "flow-edges-" + Guid.NewGuid();
        var edgeId = "edge-" + Guid.NewGuid();

        var saveRequest = new SaveWorkflowEdgesRequest
        {
            FlowId = flowId
        };
        saveRequest.Edges.Add(new WorkflowEdge
        {
            Id = edgeId,
            FlowId = flowId,
            SourceNodeId = "source-1",
            TargetNodeId = "target-1"
        });

        // Act
        var saveResponse = await this.grpcClient.SaveWorkflowEdgesAsync(saveRequest);

        // Assert
        saveResponse.Should().NotBeNull();
        saveResponse.Success.Should().BeTrue();

        var getResponse = await this.grpcClient.GetWorkflowEdgesAsync(new GetWorkflowEdgesRequest
        {
            FlowId = flowId
        });

        getResponse.Should().NotBeNull();
        getResponse.Success.Should().BeTrue();
        getResponse.Edges.Should().ContainSingle(e => e.Id == edgeId);
        getResponse.Edges[0].SourceNodeId.Should().Be("source-1");
        getResponse.Edges[0].TargetNodeId.Should().Be("target-1");
    }

    [Fact]
    public async Task ExecuteWorkflow_ShouldReturnStartedExecutionId()
    {
        // Arrange
        var request = new ExecuteWorkflowRequest
        {
            FlowId = "flow-exec-1",
            InputMessage = "Analyze test input"
        };

        // Act
        var response = await this.grpcClient.ExecuteWorkflowAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.ExecutionId.Should().NotBeNullOrWhiteSpace();
        response.ResultMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetExplorer_ShouldReturnExplorerItems()
    {
        // Arrange
        var request = new GetExplorerRequest
        {
            Path = "/"
        };

        // Act
        var response = await this.grpcClient.GetExplorerAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Items.Should().NotBeEmpty();
        response.Items.Should().Contain(i => i.Path == "/default-flow");
        response.Items.Should().Contain(i => i.Path == "/multi-agent-research");
        response.Items.Should().Contain(i => i.Path == "/gemini-assistant-flow");
        response.Items.Should().Contain(i => i.Path == "/code-review-flow");
        response.Items.Should().Contain(i => i.Path == "/mcp-calculator-time-flow");

        var defaultFlowNodes = await this.grpcClient.GetAgentNodesAsync(new GetAgentNodesRequest { FlowId = "default-flow" });
        defaultFlowNodes.Nodes.Should().NotBeEmpty();
        defaultFlowNodes.Nodes.Should().Contain(n => n.Type == NodeType.Model);
        defaultFlowNodes.Nodes.Should().Contain(n => n.Type == NodeType.Prompt);
        defaultFlowNodes.Nodes.Should().Contain(n => n.Type == NodeType.McpConnector);
        defaultFlowNodes.Nodes.Should().Contain(n => n.Type == NodeType.Agent);

        var defaultFlowEdges = await this.grpcClient.GetWorkflowEdgesAsync(new GetWorkflowEdgesRequest { FlowId = "default-flow" });
        defaultFlowEdges.Edges.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task HealthAndMetricsEndpoints_ShouldReturnValidPayloads()
    {
        // Arrange
        using var httpClient = this.factory.CreateDefaultClient();

        // Act
        var healthResponse = await httpClient.GetAsync("/health");
        var metricsResponse = await httpClient.GetAsync("/metrics");
        var telemetryResponse = await httpClient.GetAsync("/api/telemetry");

        // Assert
        healthResponse.IsSuccessStatusCode.Should().BeTrue();
        metricsResponse.IsSuccessStatusCode.Should().BeTrue();
        telemetryResponse.IsSuccessStatusCode.Should().BeTrue();

        var metricsText = await metricsResponse.Content.ReadAsStringAsync();
        metricsText.Should().Contain("agentstudio_workflows_started_total");
        metricsText.Should().Contain("agentstudio_nodes_saved_total");
    }
}
