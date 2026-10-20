using AgentStudio.Service.Protos;
using AgentStudio.Service.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Xunit;

namespace AgentStudio.Service.Tests.IntegrationTests;

public class SignalREventsTests : IClassFixture<AgentStudioTestFactory>
{
    private readonly AgentStudioTestFactory factory;
    private readonly AgentFlowService.AgentFlowServiceClient grpcClient;

    public SignalREventsTests(AgentStudioTestFactory factory)
    {
        this.factory = factory;
        this.grpcClient = factory.CreateGrpcClient();
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
    public async Task When_ExecuteWorkflow_Called_SignalR_Bus_Should_Receive_WorkflowStarted_Event()
    {
        // Arrange
        await using var hubConnection = this.factory.CreateSignalRConnection();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("WorkflowStarted", payload =>
        {
            tcs.TrySetResult(payload);
        });

        await hubConnection.StartAsync();
        hubConnection.State.Should().Be(HubConnectionState.Connected);

        var flowId = "flow-signalr-exec-" + Guid.NewGuid();
        var request = new ExecuteWorkflowRequest
        {
            FlowId = flowId,
            InputMessage = "Test workflow execution"
        };

        // Act
        var grpcResponse = await this.grpcClient.ExecuteWorkflowAsync(request);
        grpcResponse.Success.Should().BeTrue();

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completedTask.Should().Be(tcs.Task, "SignalR client should receive 'WorkflowStarted' event within timeout");

        var eventPayload = await tcs.Task;
        eventPayload.GetProperty("executionId").GetString().Should().Be(grpcResponse.ExecutionId);

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
