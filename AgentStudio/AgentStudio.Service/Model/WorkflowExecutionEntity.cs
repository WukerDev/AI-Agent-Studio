namespace AgentStudio.Service.Model;

public class WorkflowExecutionEntity
{
    public string Id { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ResultMessage { get; set; }
    public string? LogsJson { get; set; }
}
