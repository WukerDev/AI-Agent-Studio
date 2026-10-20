namespace AgentStudio.Service.Model;

public class AgentNodeEntity
{
    public string Id { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelName { get; set; }
    public double? Temperature { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Endpoint { get; set; }
    public string? McpServerUrl { get; set; }
    public string? McpAuthToken { get; set; }
    public List<string>? McpEnabledTools { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
