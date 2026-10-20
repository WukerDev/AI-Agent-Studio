using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgentStudio.Service.Protos;
using AgentStudio.Service.Model;
using AgentStudio.Service.Infrastructure;

namespace AgentStudio.Service.Application;

public class AgentFlowServiceImplementation : AgentFlowService.AgentFlowServiceBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly IUserContext userContext;
    private readonly IHubContext<EventsHub> hubContext;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<AgentFlowServiceImplementation> logger;

    public AgentFlowServiceImplementation(
        ApplicationDbContext dbContext,
        IUserContext userContext,
        IHubContext<EventsHub> hubContext,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AgentFlowServiceImplementation> logger)
    {
        this.dbContext = dbContext;
        this.userContext = userContext;
        this.hubContext = hubContext;
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
    }

    public override async Task<SaveAgentNodeResponse> SaveAgentNode(SaveAgentNodeRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var nodeId = string.IsNullOrEmpty(request.Node.Id) ? Guid.NewGuid().ToString() : request.Node.Id;
        var flowId = !string.IsNullOrEmpty(request.FlowId)
            ? request.FlowId
            : (!string.IsNullOrEmpty(request.Node.FlowId) ? request.Node.FlowId : "default-flow");

        var workflow = await this.dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == flowId && w.OwnerId == userId);
        if (workflow == null)
        {
            this.dbContext.Workflows.Add(new WorkflowEntity
            {
                Id = flowId,
                OwnerId = userId,
                Name = flowId,
                Description = "Workflow " + flowId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var entity = await this.dbContext.AgentNodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.OwnerId == userId);

        if (entity == null)
        {
            entity = new AgentNodeEntity
            {
                Id = nodeId,
                OwnerId = userId,
                FlowId = flowId
            };
            this.dbContext.AgentNodes.Add(entity);
        }
        else
        {
            entity.FlowId = flowId;
        }

        entity.Name = request.Node.Name;
        entity.Type = (int)request.Node.Type;
        entity.PositionX = request.Node.Position?.X ?? 0;
        entity.PositionY = request.Node.Position?.Y ?? 0;
        
        if (request.Node.ModelConfig != null)
        {
            entity.ModelProvider = request.Node.ModelConfig.Provider;
            entity.ModelName = request.Node.ModelConfig.ModelName;
            entity.Temperature = request.Node.ModelConfig.Temperature;
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

        await this.hubContext.Clients.User(userId).SendAsync("NodeSaved", new { NodeId = entity.Id, FlowId = entity.FlowId });

        return new SaveAgentNodeResponse
        {
            Id = entity.Id,
            Success = true
        };
    }

    public override async Task<GetAgentNodesResponse> GetAgentNodes(GetAgentNodesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        var entities = await this.dbContext.AgentNodes
            .Where(n => n.OwnerId == userId && n.FlowId == flowId)
            .ToListAsync();

        var response = new GetAgentNodesResponse
        {
            Success = true,
            Message = $"Found {entities.Count} nodes"
        };

        var nodesPayload = new List<object>();

        foreach (var entity in entities)
        {
            var nodeProto = new AgentNode
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = (NodeType)entity.Type,
                Position = new Position { X = entity.PositionX, Y = entity.PositionY },
                SystemPrompt = entity.SystemPrompt,
                Endpoint = entity.Endpoint,
                FlowId = entity.FlowId,
                ModelConfig = new ModelConfig
                {
                    Provider = entity.ModelProvider,
                    ModelName = entity.ModelName,
                    Temperature = (float)(entity.Temperature ?? 0.7f)
                },
                McpConfig = new McpConfig
                {
                    ServerUrl = entity.McpServerUrl,
                    AuthToken = entity.McpAuthToken
                }
            };

            if (entity.McpEnabledTools != null)
            {
                nodeProto.McpConfig.EnabledTools.AddRange(entity.McpEnabledTools);
            }

            response.Nodes.Add(nodeProto);

            nodesPayload.Add(new
            {
                id = entity.Id,
                name = entity.Name,
                type = entity.Type,
                position = new { x = entity.PositionX, y = entity.PositionY },
                modelConfig = new
                {
                    provider = entity.ModelProvider,
                    modelName = entity.ModelName,
                    temperature = entity.Temperature
                },
                systemPrompt = entity.SystemPrompt,
                endpoint = entity.Endpoint,
                mcpConfig = new
                {
                    serverUrl = entity.McpServerUrl,
                    authToken = entity.McpAuthToken,
                    enabledTools = entity.McpEnabledTools
                },
                flowId = entity.FlowId
            });
        }

        await this.hubContext.Clients.User(userId).SendAsync("AgentNodesLoaded", new { FlowId = flowId, Nodes = nodesPayload });

        return response;
    }

    public override async Task<SaveWorkflowEdgesResponse> SaveWorkflowEdges(SaveWorkflowEdgesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        var workflow = await this.dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == flowId && w.OwnerId == userId);
        if (workflow == null)
        {
            this.dbContext.Workflows.Add(new WorkflowEntity
            {
                Id = flowId,
                OwnerId = userId,
                Name = flowId,
                Description = "Workflow " + flowId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var existingEdges = await this.dbContext.WorkflowEdges
            .Where(e => e.FlowId == flowId)
            .ToListAsync();

        this.dbContext.WorkflowEdges.RemoveRange(existingEdges);

        foreach (var edge in request.Edges)
        {
            this.dbContext.WorkflowEdges.Add(new WorkflowEdgeEntity
            {
                Id = string.IsNullOrEmpty(edge.Id) ? Guid.NewGuid().ToString() : edge.Id,
                FlowId = flowId,
                OwnerId = userId,
                SourceNodeId = edge.SourceNodeId,
                SourceHandle = edge.SourceHandle,
                TargetNodeId = edge.TargetNodeId,
                TargetHandle = edge.TargetHandle
            });
        }

        await this.dbContext.SaveChangesAsync();

        await this.hubContext.Clients.User(userId).SendAsync("WorkflowEdgesSaved", new { FlowId = flowId, Count = request.Edges.Count });

        return new SaveWorkflowEdgesResponse { Success = true };
    }

    public override async Task<GetWorkflowEdgesResponse> GetWorkflowEdges(GetWorkflowEdgesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        var entities = await this.dbContext.WorkflowEdges
            .Where(e => e.FlowId == flowId)
            .ToListAsync();

        var response = new GetWorkflowEdgesResponse { Success = true };
        var edgesPayload = new List<object>();

        foreach (var entity in entities)
        {
            response.Edges.Add(new WorkflowEdge
            {
                Id = entity.Id,
                FlowId = entity.FlowId,
                SourceNodeId = entity.SourceNodeId,
                SourceHandle = entity.SourceHandle ?? string.Empty,
                TargetNodeId = entity.TargetNodeId,
                TargetHandle = entity.TargetHandle ?? string.Empty
            });

            edgesPayload.Add(new
            {
                id = entity.Id,
                flowId = entity.FlowId,
                sourceNodeId = entity.SourceNodeId,
                sourceHandle = entity.SourceHandle,
                targetNodeId = entity.TargetNodeId,
                targetHandle = entity.TargetHandle
            });
        }

        await this.hubContext.Clients.User(userId).SendAsync("WorkflowEdgesLoaded", new { FlowId = flowId, Edges = edgesPayload });

        return response;
    }

    public override async Task<ExecuteWorkflowResponse> ExecuteWorkflow(ExecuteWorkflowRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var executionId = Guid.NewGuid().ToString();
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        await this.hubContext.Clients.User(userId).SendAsync("WorkflowStarted", new { ExecutionId = executionId, FlowId = flowId });

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = this.serviceScopeFactory.CreateScope();
                var agentEngine = scope.ServiceProvider.GetRequiredService<IAgentEngine>();
                await agentEngine.ExecuteFlowAsync(flowId, userId, request.InputMessage);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Background agent execution failed for execution {ExecutionId}", executionId);
            }
        });

        return new ExecuteWorkflowResponse
        {
            Success = true,
            ExecutionId = executionId,
            ResultMessage = "Workflow execution dispatched"
        };
    }

    public override async Task<GetExplorerResponse> GetExplorer(GetExplorerRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var items = new List<ExplorerItem>();

        var flows = await this.dbContext.Workflows
            .Where(w => w.OwnerId == userId)
            .ToListAsync();

        foreach (var flow in flows)
        {
            items.Add(new ExplorerItem
            {
                Name = $"{flow.Name}.agentflow",
                Path = $"/{flow.Id}",
                Type = ItemType.File
            });
        }

        if (items.Count == 0)
        {
            items.Add(new ExplorerItem
            {
                Name = "DefaultWorkflow.agentflow",
                Path = "/default-flow",
                Type = ItemType.File
            });
        }

        var response = new GetExplorerResponse
        {
            Success = true,
            Message = "Explorer loaded"
        };
        response.Items.AddRange(items);

        var payload = items.Select(i => new { name = i.Name, path = i.Path, type = (int)i.Type }).ToList();
        await this.hubContext.Clients.User(userId).SendAsync("ExplorerLoaded", new { Path = request.Path, Items = payload });

        return response;
    }
}
