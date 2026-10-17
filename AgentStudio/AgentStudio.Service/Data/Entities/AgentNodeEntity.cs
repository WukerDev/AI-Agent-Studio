namespace AgentStudio.Service.Data.Entities;

public class AgentNodeEntity
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string FlowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string ModelProvider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public float ModelTemperature { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string McpServerUrl { get; set; } = string.Empty;
    public string McpAuthToken { get; set; } = string.Empty;
    public List<string> McpEnabledTools { get; set; } = new();
}