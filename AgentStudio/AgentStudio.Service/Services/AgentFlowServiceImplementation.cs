using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using AgentStudio.Service.Protos;
using AgentStudio.Service.Data;
using AgentStudio.Service.Data.Entities;
using AgentStudio.Service.Security;
using AgentStudio.Service.Hubs;

namespace AgentStudio.Service.Services;

public class AgentFlowServiceImplementation : AgentFlowService.AgentFlowServiceBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly IUserContext userContext;
    private readonly IHubContext<EventsHub> hubContext;

    public AgentFlowServiceImplementation(
        ApplicationDbContext dbContext,
        IUserContext userContext,
        IHubContext<EventsHub> hubContext)
    {
        this.dbContext = dbContext;
        this.userContext = userContext;
        this.hubContext = hubContext;
    }

    public override async Task<SaveAgentNodeResponse> SaveAgentNode(SaveAgentNodeRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var nodeId = string.IsNullOrEmpty(request.Node.Id) ? Guid.NewGuid().ToString() : request.Node.Id;

        var entity = await this.dbContext.AgentNodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.OwnerId == userId);

        if (entity == null)
        {
            entity = new AgentNodeEntity
            {
                Id = nodeId,
                OwnerId = userId
            };
            this.dbContext.AgentNodes.Add(entity);
        }

        entity.Name = request.Node.Name;
        entity.Type = (int)request.Node.Type;
        entity.PositionX = request.Node.Position?.X ?? 0;
        entity.PositionY = request.Node.Position?.Y ?? 0;
        
        if (request.Node.ModelConfig != null)
        {
            entity.ModelProvider = request.Node.ModelConfig.Provider;
            entity.ModelName = request.Node.ModelConfig.ModelName;
            entity.ModelTemperature = request.Node.ModelConfig.Temperature;
        }

        entity.SystemPrompt = request.Node.SystemPrompt;
        entity.Endpoint = request.Node.Endpoint;

        if (request.Node.McpConfig != null)
        {
            entity.McpServerUrl = request.Node.McpConfig.ServerUrl;
            entity.McpAuthToken = request.Node.McpConfig.AuthToken;
            entity.McpEnabledTools = request.Node.McpConfig.EnabledTools.ToList();
        }

        await this.dbContext.SaveChangesAsync();

        await this.hubContext.Clients.User(userId).SendAsync("NodeSaved", new { NodeId = entity.Id });

        return new SaveAgentNodeResponse
        {
            Id = entity.Id,
            Success = true
        };
    }

    public override async Task<GetAgentNodesResponse> GetAgentNodes(GetAgentNodesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var entities = await this.dbContext.AgentNodes
            .Where(n => n.OwnerId == userId && n.FlowId == request.FlowId)
            .ToListAsync();

        var nodes = new List<object>();

        foreach (var entity in entities)
        {
            nodes.Add(new
            {
                id = entity.Id,
                name = entity.Name,
                type = entity.Type,
                position = new { x = entity.PositionX, y = entity.PositionY },
                modelConfig = new
                {
                    provider = entity.ModelProvider,
                    modelName = entity.ModelName,
                    temperature = entity.ModelTemperature
                },
                systemPrompt = entity.SystemPrompt,
                endpoint = entity.Endpoint,
                mcpConfig = new
                {
                    serverUrl = entity.McpServerUrl,
                    authToken = entity.McpAuthToken,
                    enabledTools = entity.McpEnabledTools
                }
            });
        }

        await this.hubContext.Clients.User(userId).SendAsync("AgentNodesLoaded", new { FlowId = request.FlowId, Nodes = nodes });

        return new GetAgentNodesResponse
        {
            Success = true,
            Message = "Data dispatched via events"
        };
    }

    public override async Task<ExecuteWorkflowResponse> ExecuteWorkflow(ExecuteWorkflowRequest request, ServerCallContext context)
    {
        var executionId = Guid.NewGuid().ToString();

        await this.hubContext.Clients.User(this.userContext.UserId).SendAsync("WorkflowStarted", new { ExecutionId = executionId });

        return new ExecuteWorkflowResponse
        {
            Success = true,
            ExecutionId = executionId,
            ResultMessage = "Started"
        };
    }

    public override async Task<GetExplorerResponse> GetExplorer(GetExplorerRequest request, ServerCallContext context)
    {
        var items = new List<object>();

        await this.hubContext.Clients.User(this.userContext.UserId).SendAsync("ExplorerLoaded", new { Path = request.Path, Items = items });

        return new GetExplorerResponse
        {
            Success = true,
            Message = "Data dispatched via events"
        };
    }
}