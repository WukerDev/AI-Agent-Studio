namespace AgentStudio.Service.Application;

public interface IAgentEngine
{
    Task<WorkflowExecutionResult> ExecuteFlowAsync(string flowId, string userId, string? inputMessage = null, CancellationToken cancellationToken = default);
}

public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public string ExecutionId { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
