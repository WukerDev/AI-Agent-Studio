using Grpc.Core;
using AgentStudio.Service.Protos;

namespace AgentStudio.Service.Services;

public class AgentFlowServiceImplementation : AgentFlowService.AgentFlowServiceBase
{
    public override Task<ExecuteWorkflowResponse> ExecuteWorkflow(ExecuteWorkflowRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ExecuteWorkflowResponse
        {
            Success = true,
            ExecutionId = Guid.NewGuid().ToString(),
            ResultMessage = "Started"
        });
    }

    public override Task<GetExplorerResponse> GetExplorer(GetExplorerRequest request, ServerCallContext context)
    {
        var response = new GetExplorerResponse();
        return Task.FromResult(response);
    }

    public override Task<SaveAgentNodeResponse> SaveAgentNode(SaveAgentNodeRequest request, ServerCallContext context)
    {
        return Task.FromResult(new SaveAgentNodeResponse
        {
            Id = request.Node?.Id ?? Guid.NewGuid().ToString(),
            Success = true
        });
    }

    public override Task<GetAgentNodesResponse> GetAgentNodes(GetAgentNodesRequest request, ServerCallContext context)
    {
        var response = new GetAgentNodesResponse();
        return Task.FromResult(response);
    }
}