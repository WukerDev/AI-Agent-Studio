using AgentStudio.Service.Protos;
using AgentStudio.Service.Tests.Infrastructure;
using FluentAssertions;
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
                    ModelName = "llama3.2",
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
        retrieved.McpConfig.ServerUrl.Should().Be("http://localhost:8000/mcp");
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
    }
}
