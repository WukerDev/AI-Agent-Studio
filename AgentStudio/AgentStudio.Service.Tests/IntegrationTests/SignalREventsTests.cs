using AgentStudio.Service.Protos;
using AgentStudio.Service.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AgentStudio.Service.Tests.IntegrationTests;

public class SignalREventsTests : IClassFixture<AgentStudioTestFactory>
{
    private readonly AgentStudioTestFactory factory;
    private readonly AgentFlowService.AgentFlowServiceClient grpcClient;
    private readonly ITestOutputHelper output;

    public SignalREventsTests(AgentStudioTestFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        this.grpcClient = factory.CreateGrpcClient();
        this.output = output;
    }

    [Fact]
    public async Task When_SaveAgentNode_Called_SignalR_Bus_Should_Receive_NodeSaved_Event()
    {
        // Arrange
        await using var hubConnection = this.factory.CreateSignalRConnection();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("NodeSaved", payload =>
        {
            tcs.TrySetResult(payload);
        });

        await hubConnection.StartAsync();
        hubConnection.State.Should().Be(HubConnectionState.Connected);

        var testNodeId = "node-" + Guid.NewGuid();
        var request = new SaveAgentNodeRequest
        {
            FlowId = "flow-signalr-1",
            Node = new AgentNode
            {
                Id = testNodeId,
                Name = "Realtime SignalR Agent",
                Type = NodeType.Agent,
                Position = new Position { X = 50, Y = 50 }
            }
        };

        // Act
        var grpcResponse = await this.grpcClient.SaveAgentNodeAsync(request);
        grpcResponse.Success.Should().BeTrue();

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completedTask.Should().Be(tcs.Task, "SignalR client should receive 'NodeSaved' event within timeout");

        var eventPayload = await tcs.Task;
        eventPayload.GetProperty("nodeId").GetString().Should().Be(testNodeId);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task When_DeleteAgentNode_Called_SignalR_Bus_Should_Receive_NodeDeleted_Event()
    {
        // Arrange
        await using var hubConnection = this.factory.CreateSignalRConnection();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("NodeDeleted", payload =>
        {
            tcs.TrySetResult(payload);
        });

        await hubConnection.StartAsync();

        var flowId = "flow-signalr-del-" + Guid.NewGuid();
        var nodeId = "node-del-" + Guid.NewGuid();

        await this.grpcClient.SaveAgentNodeAsync(new SaveAgentNodeRequest
        {
            FlowId = flowId,
            Node = new AgentNode
            {
                Id = nodeId,
                Name = "Node To Delete",
                Type = NodeType.Agent,
                Position = new Position { X = 25, Y = 25 }
            }
        });

        // Act
        var deleteResponse = await this.grpcClient.DeleteAgentNodeAsync(new DeleteAgentNodeRequest
        {
            FlowId = flowId,
            Id = nodeId
        });
        deleteResponse.Success.Should().BeTrue();

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completedTask.Should().Be(tcs.Task, "SignalR client should receive 'NodeDeleted' event within timeout");

        var eventPayload = await tcs.Task;
        eventPayload.GetProperty("nodeId").GetString().Should().Be(nodeId);
        eventPayload.GetProperty("flowId").GetString().Should().Be(flowId);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task When_ExecuteWorkflow_Called_SignalR_Bus_Should_Receive_WorkflowStarted_And_Finished_With_Gemma3_12b()
    {
        // Arrange
        await using var hubConnection = this.factory.CreateSignalRConnection();
        var startedTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishedTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        string? expectedExecutionId = null;

        hubConnection.On<JsonElement>("WorkflowStarted", payload =>
        {
            startedTcs.TrySetResult(payload);
        });

        hubConnection.On<JsonElement>("WorkflowExecutionFinished", payload =>
        {
            if (expectedExecutionId == null || payload.GetProperty("executionId").GetString() == expectedExecutionId)
            {
                finishedTcs.TrySetResult(payload);
            }
        });

        await hubConnection.StartAsync();
        hubConnection.State.Should().Be(HubConnectionState.Connected);

        var flowId = "flow-signalr-exec-" + Guid.NewGuid();

        await this.grpcClient.SaveAgentNodeAsync(new SaveAgentNodeRequest
        {
            FlowId = flowId,
            Node = new AgentNode
            {
                Id = "agent-" + Guid.NewGuid(),
                Name = "Gemma3 Realtime Agent",
                Type = NodeType.Agent,
                Position = new Position { X = 100, Y = 100 },
                SystemPrompt = "You are a helpful assistant. Reply with the single word: OK.",
                Endpoint = "http://localhost:11434",
                ModelConfig = new ModelConfig
                {
                    Provider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.1f
                }
            }
        });

        var request = new ExecuteWorkflowRequest
        {
            FlowId = flowId,
            InputMessage = "Reply with OK."
        };

        // Act
        var grpcResponse = await this.grpcClient.ExecuteWorkflowAsync(request);
        grpcResponse.Success.Should().BeTrue();
        expectedExecutionId = grpcResponse.ExecutionId;

        // Assert WorkflowStarted
        var completedStart = await Task.WhenAny(startedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completedStart.Should().Be(startedTcs.Task, "SignalR client should receive 'WorkflowStarted' event within timeout");

        var startPayload = await startedTcs.Task;
        startPayload.GetProperty("executionId").GetString().Should().Be(grpcResponse.ExecutionId);

        // Assert WorkflowExecutionFinished (from live Ollama gemma3:12b)
        var completedFinish = await Task.WhenAny(finishedTcs.Task, Task.Delay(TimeSpan.FromSeconds(45)));
        completedFinish.Should().Be(finishedTcs.Task, "SignalR client should receive 'WorkflowExecutionFinished' event from gemma3:12b");

        var finishPayload = await finishedTcs.Task;
        finishPayload.GetProperty("executionId").GetString().Should().Be(grpcResponse.ExecutionId);
        if (await OllamaTestHelper.IsOllamaGemmaAvailableAsync())
        {
            finishPayload.GetProperty("success").GetBoolean().Should().BeTrue();
            finishPayload.GetProperty("output").GetString().Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            var warnMsg = "[WARN] SignalREventsTests: Ollama (gemma3:12b) nie jest uruchomione pod adresem http://localhost:11434. Zweryfikowano poprawny broadcast zdarzenia WorkflowExecutionFinished z flagą błędu w środowisku CI/CD.";
            this.output.WriteLine($"##[warning]{warnMsg}");
            this.output.WriteLine(warnMsg);
            Console.WriteLine(warnMsg);
            finishPayload.GetProperty("success").GetBoolean().Should().BeFalse();
            finishPayload.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task When_GetAgentNodes_Called_SignalR_Bus_Should_Receive_AgentNodesLoaded_Event()
    {
        // Arrange
        await using var hubConnection = this.factory.CreateSignalRConnection();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("AgentNodesLoaded", payload =>
        {
            tcs.TrySetResult(payload);
        });

        await hubConnection.StartAsync();

        var flowId = "flow-load-" + Guid.NewGuid();

        // Act
        var grpcResponse = await this.grpcClient.GetAgentNodesAsync(new GetAgentNodesRequest
        {
            FlowId = flowId
        });
        grpcResponse.Success.Should().BeTrue();

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completedTask.Should().Be(tcs.Task, "SignalR client should receive 'AgentNodesLoaded' event within timeout");

        var eventPayload = await tcs.Task;
        eventPayload.GetProperty("flowId").GetString().Should().Be(flowId);

        await hubConnection.StopAsync();
    }
}
