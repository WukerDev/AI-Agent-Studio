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

        var workflow = await this.dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == flowId);
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
        else
        {
            workflow.UpdatedAt = DateTime.UtcNow;
        }

        var entity = await this.dbContext.AgentNodes.FirstOrDefaultAsync(n => n.Id == nodeId);

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
            entity.OwnerId = userId;
        }

        entity.Name = request.Node.Name ?? string.Empty;
        entity.Type = (int)request.Node.Type;
        entity.PositionX = request.Node.Position?.X ?? 0;
        entity.PositionY = request.Node.Position?.Y ?? 0;
        entity.UpdatedAt = DateTime.UtcNow;

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

        AgentStudioTelemetry.RecordNodeSaved(flowId, entity.Type);

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

        if (entities.Count == 0 && (userId != "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
        {
            entities = await this.dbContext.AgentNodes
                .Where(n => n.FlowId == flowId && (n.OwnerId == "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
                .ToListAsync();
        }

        var response = new GetAgentNodesResponse
        {
            Success = true,
            Message = $"Found {entities.Count} nodes"
        };

        var nodesPayload = new List<object>();

        foreach (var entity in entities)
        {
            var enabledTools = entity.McpEnabledTools ?? new List<string>();

            var nodeProto = new AgentNode
            {
                Id = entity.Id ?? string.Empty,
                Name = entity.Name ?? string.Empty,
                Type = (NodeType)entity.Type,
                Position = new Position { X = entity.PositionX, Y = entity.PositionY },
                SystemPrompt = entity.SystemPrompt ?? string.Empty,
                Endpoint = entity.Endpoint ?? string.Empty,
                FlowId = entity.FlowId ?? flowId,
                ModelConfig = new ModelConfig
                {
                    Provider = entity.ModelProvider ?? string.Empty,
                    ModelName = entity.ModelName ?? string.Empty,
                    Temperature = (float)(entity.Temperature ?? 0.7f)
                },
                McpConfig = new McpConfig
                {
                    ServerUrl = entity.McpServerUrl ?? string.Empty,
                    AuthToken = entity.McpAuthToken ?? string.Empty
                }
            };

            if (enabledTools.Count > 0)
            {
                nodeProto.McpConfig.EnabledTools.AddRange(enabledTools);
            }

            response.Nodes.Add(nodeProto);

            nodesPayload.Add(new
            {
                id = entity.Id ?? string.Empty,
                name = entity.Name ?? string.Empty,
                type = entity.Type,
                position = new { x = entity.PositionX, y = entity.PositionY },
                modelConfig = new
                {
                    provider = entity.ModelProvider ?? string.Empty,
                    modelName = entity.ModelName ?? string.Empty,
                    temperature = entity.Temperature ?? 0.7
                },
                systemPrompt = entity.SystemPrompt ?? string.Empty,
                endpoint = entity.Endpoint ?? string.Empty,
                mcpConfig = new
                {
                    serverUrl = entity.McpServerUrl ?? string.Empty,
                    authToken = entity.McpAuthToken ?? string.Empty,
                    enabledTools
                },
                flowId = entity.FlowId ?? flowId
            });
        }

        await this.hubContext.Clients.User(userId).SendAsync("AgentNodesLoaded", new { FlowId = flowId, Nodes = nodesPayload });

        return response;
    }

    public override async Task<DeleteAgentNodeResponse> DeleteAgentNode(DeleteAgentNodeRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        if (!string.IsNullOrEmpty(request.Id))
        {
            var node = await this.dbContext.AgentNodes
                .FirstOrDefaultAsync(n => n.Id == request.Id && (n.OwnerId == userId || n.OwnerId == "guest-user"));

            if (node != null)
            {
                flowId = node.FlowId;
                this.dbContext.AgentNodes.Remove(node);
            }

            var connectedEdges = await this.dbContext.WorkflowEdges
                .Where(e => (e.OwnerId == userId || e.OwnerId == "guest-user") && (e.SourceNodeId == request.Id || e.TargetNodeId == request.Id))
                .ToListAsync();

            if (connectedEdges.Count > 0)
            {
                this.dbContext.WorkflowEdges.RemoveRange(connectedEdges);
            }

            await this.dbContext.SaveChangesAsync();

            AgentStudioTelemetry.RecordNodeDeleted(flowId);

            await this.hubContext.Clients.User(userId).SendAsync("NodeDeleted", new { NodeId = request.Id, FlowId = flowId });
        }

        return new DeleteAgentNodeResponse { Success = true };
    }

    public override async Task<SaveWorkflowEdgesResponse> SaveWorkflowEdges(SaveWorkflowEdgesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        var workflow = await this.dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == flowId);
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
        else
        {
            workflow.UpdatedAt = DateTime.UtcNow;
        }

        var existingEdges = await this.dbContext.WorkflowEdges
            .Where(e => e.FlowId == flowId && (e.OwnerId == userId || e.OwnerId == "guest-user"))
            .ToListAsync();

        this.dbContext.WorkflowEdges.RemoveRange(existingEdges);

        foreach (var edge in request.Edges)
        {
            var edgeId = string.IsNullOrEmpty(edge.Id) ? Guid.NewGuid().ToString() : edge.Id;
            this.dbContext.WorkflowEdges.Add(new WorkflowEdgeEntity
            {
                Id = edgeId,
                FlowId = flowId,
                OwnerId = userId,
                SourceNodeId = edge.SourceNodeId ?? string.Empty,
                SourceHandle = edge.SourceHandle ?? string.Empty,
                TargetNodeId = edge.TargetNodeId ?? string.Empty,
                TargetHandle = edge.TargetHandle ?? string.Empty
            });
        }

        await this.dbContext.SaveChangesAsync();

        AgentStudioTelemetry.RecordEdgesSaved(flowId, request.Edges.Count);

        await this.hubContext.Clients.User(userId).SendAsync("WorkflowEdgesSaved", new { FlowId = flowId, Count = request.Edges.Count });

        return new SaveWorkflowEdgesResponse { Success = true };
    }

    public override async Task<GetWorkflowEdgesResponse> GetWorkflowEdges(GetWorkflowEdgesRequest request, ServerCallContext context)
    {
        var userId = this.userContext.UserId;
        var flowId = string.IsNullOrEmpty(request.FlowId) ? "default-flow" : request.FlowId;

        var entities = await this.dbContext.WorkflowEdges
            .Where(e => e.FlowId == flowId && e.OwnerId == userId)
            .ToListAsync();

        if (entities.Count == 0 && (userId != "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
        {
            entities = await this.dbContext.WorkflowEdges
                .Where(e => e.FlowId == flowId && (e.OwnerId == "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
                .ToListAsync();
        }

        var response = new GetWorkflowEdgesResponse { Success = true };
        var edgesPayload = new List<object>();

        foreach (var entity in entities)
        {
            response.Edges.Add(new WorkflowEdge
            {
                Id = entity.Id ?? string.Empty,
                FlowId = entity.FlowId ?? flowId,
                SourceNodeId = entity.SourceNodeId ?? string.Empty,
                SourceHandle = entity.SourceHandle ?? string.Empty,
                TargetNodeId = entity.TargetNodeId ?? string.Empty,
                TargetHandle = entity.TargetHandle ?? string.Empty
            });

            edgesPayload.Add(new
            {
                id = entity.Id ?? string.Empty,
                flowId = entity.FlowId ?? flowId,
                sourceNodeId = entity.SourceNodeId ?? string.Empty,
                sourceHandle = entity.SourceHandle ?? string.Empty,
                targetNodeId = entity.TargetNodeId ?? string.Empty,
                targetHandle = entity.TargetHandle ?? string.Empty
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
                await agentEngine.ExecuteFlowAsync(flowId, userId, request.InputMessage, executionId: executionId);
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
            .Where(w => w.OwnerId == userId || w.OwnerId == "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(w.Id))
            .OrderBy(w => w.Name)
            .ToListAsync();

        foreach (var flow in flows)
        {
            var displayName = flow.Name.EndsWith(".agentflow", StringComparison.OrdinalIgnoreCase)
                ? flow.Name
                : $"{flow.Name}.agentflow";

            items.Add(new ExplorerItem
            {
                Name = displayName,
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
        await this.hubContext.Clients.User(userId).SendAsync("ExplorerLoaded", new { Path = request.Path ?? "/", Items = payload });

        return response;
    }
}
