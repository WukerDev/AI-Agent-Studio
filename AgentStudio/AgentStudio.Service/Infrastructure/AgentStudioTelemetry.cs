using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace AgentStudio.Service.Infrastructure;

public static class AgentStudioTelemetry
{
    public const string ServiceName = "AgentStudio.Service";
    public const string MeterName = "AgentStudio.Service.Metrics";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "2.0.0");
    public static readonly Meter Meter = new(MeterName, "2.0.0");

    private static long workflowsStartedCount;
    private static long workflowsCompletedCount;
    private static long workflowsFailedCount;
    private static long nodesSavedCount;
    private static long nodesDeletedCount;
    private static long edgesSavedCount;
    private static long mcpToolsAttachedCount;
    private static double lastExecutionDurationMs;

    public static readonly Counter<long> WorkflowsStartedCounter =
        Meter.CreateCounter<long>("agentstudio_workflows_started_total", description: "Total number of workflows started");

    public static readonly Counter<long> WorkflowsCompletedCounter =
        Meter.CreateCounter<long>("agentstudio_workflows_completed_total", description: "Total number of workflows completed successfully");

    public static readonly Counter<long> WorkflowsFailedCounter =
        Meter.CreateCounter<long>("agentstudio_workflows_failed_total", description: "Total number of failed workflow executions");

    public static readonly Histogram<double> WorkflowDurationHistogram =
        Meter.CreateHistogram<double>("agentstudio_workflow_duration_ms", unit: "ms", description: "Duration of workflow executions in milliseconds");

    public static readonly Counter<long> NodesSavedCounter =
        Meter.CreateCounter<long>("agentstudio_nodes_saved_total", description: "Total number of agent nodes saved");

    public static readonly Counter<long> NodesDeletedCounter =
        Meter.CreateCounter<long>("agentstudio_nodes_deleted_total", description: "Total number of agent nodes deleted");

    public static readonly Counter<long> EdgesSavedCounter =
        Meter.CreateCounter<long>("agentstudio_edges_saved_total", description: "Total number of workflow edges saved");

    public static readonly Counter<long> McpToolsAttachedCounter =
        Meter.CreateCounter<long>("agentstudio_mcp_tools_attached_total", description: "Total number of MCP tools dynamically attached to agents");

    public static void RecordWorkflowStarted(string flowId)
    {
        Interlocked.Increment(ref workflowsStartedCount);
        WorkflowsStartedCounter.Add(1, new KeyValuePair<string, object?>("flow_id", flowId));
    }

    public static void RecordWorkflowCompleted(string flowId, string provider, string model, double durationMs)
    {
        Interlocked.Increment(ref workflowsCompletedCount);
        Volatile.Write(ref lastExecutionDurationMs, durationMs);
        WorkflowsCompletedCounter.Add(1,
            new KeyValuePair<string, object?>("flow_id", flowId),
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("model", model));
        WorkflowDurationHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("flow_id", flowId),
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("model", model));
    }

    public static void RecordWorkflowFailed(string flowId, double durationMs)
    {
        Interlocked.Increment(ref workflowsFailedCount);
        Volatile.Write(ref lastExecutionDurationMs, durationMs);
        WorkflowsFailedCounter.Add(1, new KeyValuePair<string, object?>("flow_id", flowId));
        WorkflowDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("flow_id", flowId));
    }

    public static void RecordNodeSaved(string flowId, int nodeType)
    {
        Interlocked.Increment(ref nodesSavedCount);
        NodesSavedCounter.Add(1,
            new KeyValuePair<string, object?>("flow_id", flowId),
            new KeyValuePair<string, object?>("node_type", nodeType));
    }

    public static void RecordNodeDeleted(string flowId)
    {
        Interlocked.Increment(ref nodesDeletedCount);
        NodesDeletedCounter.Add(1, new KeyValuePair<string, object?>("flow_id", flowId));
    }

    public static void RecordEdgesSaved(string flowId, int count)
    {
        Interlocked.Add(ref edgesSavedCount, count);
        EdgesSavedCounter.Add(count, new KeyValuePair<string, object?>("flow_id", flowId));
    }

    public static void RecordMcpToolsAttached(int count)
    {
        Interlocked.Add(ref mcpToolsAttachedCount, count);
        McpToolsAttachedCounter.Add(count);
    }

    public static object GetSnapshot() => new
    {
        service = ServiceName,
        timestamp = DateTimeOffset.UtcNow,
        metrics = new
        {
            workflowsStartedTotal = Interlocked.Read(ref workflowsStartedCount),
            workflowsCompletedTotal = Interlocked.Read(ref workflowsCompletedCount),
            workflowsFailedTotal = Interlocked.Read(ref workflowsFailedCount),
            nodesSavedTotal = Interlocked.Read(ref nodesSavedCount),
            nodesDeletedTotal = Interlocked.Read(ref nodesDeletedCount),
            edgesSavedTotal = Interlocked.Read(ref edgesSavedCount),
            mcpToolsAttachedTotal = Interlocked.Read(ref mcpToolsAttachedCount),
            lastExecutionDurationMs = Volatile.Read(ref lastExecutionDurationMs)
        }
    };

    public static string GetPrometheusMetricsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP agentstudio_workflows_started_total Total number of workflows started");
        sb.AppendLine("# TYPE agentstudio_workflows_started_total counter");
        sb.AppendLine($"agentstudio_workflows_started_total {Interlocked.Read(ref workflowsStartedCount)}");

        sb.AppendLine("# HELP agentstudio_workflows_completed_total Total number of workflows completed successfully");
        sb.AppendLine("# TYPE agentstudio_workflows_completed_total counter");
        sb.AppendLine($"agentstudio_workflows_completed_total {Interlocked.Read(ref workflowsCompletedCount)}");

        sb.AppendLine("# HELP agentstudio_workflows_failed_total Total number of failed workflow executions");
        sb.AppendLine("# TYPE agentstudio_workflows_failed_total counter");
        sb.AppendLine($"agentstudio_workflows_failed_total {Interlocked.Read(ref workflowsFailedCount)}");

        sb.AppendLine("# HELP agentstudio_nodes_saved_total Total number of agent nodes saved");
        sb.AppendLine("# TYPE agentstudio_nodes_saved_total counter");
        sb.AppendLine($"agentstudio_nodes_saved_total {Interlocked.Read(ref nodesSavedCount)}");

        sb.AppendLine("# HELP agentstudio_nodes_deleted_total Total number of agent nodes deleted");
        sb.AppendLine("# TYPE agentstudio_nodes_deleted_total counter");
        sb.AppendLine($"agentstudio_nodes_deleted_total {Interlocked.Read(ref nodesDeletedCount)}");

        sb.AppendLine("# HELP agentstudio_edges_saved_total Total number of workflow edges saved");
        sb.AppendLine("# TYPE agentstudio_edges_saved_total counter");
        sb.AppendLine($"agentstudio_edges_saved_total {Interlocked.Read(ref edgesSavedCount)}");

        sb.AppendLine("# HELP agentstudio_mcp_tools_attached_total Total number of MCP tools dynamically attached");
        sb.AppendLine("# TYPE agentstudio_mcp_tools_attached_total counter");
        sb.AppendLine($"agentstudio_mcp_tools_attached_total {Interlocked.Read(ref mcpToolsAttachedCount)}");

        sb.AppendLine("# HELP agentstudio_last_execution_duration_ms Last workflow execution duration in ms");
        sb.AppendLine("# TYPE agentstudio_last_execution_duration_ms gauge");
        sb.AppendLine($"agentstudio_last_execution_duration_ms {Volatile.Read(ref lastExecutionDurationMs).ToString(CultureInfo.InvariantCulture)}");

        return sb.ToString();
    }
}
