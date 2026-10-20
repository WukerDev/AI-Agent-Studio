namespace AgentStudio.Service.Model;

public class WorkflowEdgeEntity
{
    public string Id { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourceHandle { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetHandle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
