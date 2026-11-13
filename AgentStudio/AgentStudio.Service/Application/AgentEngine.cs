using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentStudio.Service.Infrastructure;
using AgentStudio.Service.Model;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AgentStudio.Service.Application;

public class AgentEngine : IAgentEngine
{
    private readonly ApplicationDbContext dbContext;
    private readonly IMcpClientService mcpClientService;
    private readonly IHubContext<EventsHub> hubContext;
    private readonly IConfiguration configuration;
    private readonly ILogger<AgentEngine> logger;
    private readonly ISkillService skillService;

    public AgentEngine(
        ApplicationDbContext dbContext,
        IMcpClientService mcpClientService,
        IHubContext<EventsHub> hubContext,
        IConfiguration configuration,
        ILogger<AgentEngine> logger,
        ISkillService? skillService = null)
    {
        this.dbContext = dbContext;
        this.mcpClientService = mcpClientService;
        this.hubContext = hubContext;
        this.configuration = configuration;
        this.logger = logger;
        this.skillService = skillService ?? new SkillService(configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillService>.Instance);
    }

    public async Task<WorkflowExecutionResult> ExecuteFlowAsync(
        string flowId,
        string userId,
        string? inputMessage = null,
        CancellationToken cancellationToken = default,
        string? executionId = null)
    {
        using var activity = AgentStudioTelemetry.ActivitySource.StartActivity("ExecuteFlow");
        var stopwatch = Stopwatch.StartNew();

        var resolvedExecutionId = !string.IsNullOrWhiteSpace(executionId)
            ? executionId
            : Guid.NewGuid().ToString();

        activity?.SetTag("flow.id", flowId);
        activity?.SetTag("execution.id", resolvedExecutionId);
        AgentStudioTelemetry.RecordWorkflowStarted(flowId);

        var execution = new WorkflowExecutionEntity
        {
            Id = resolvedExecutionId,
            FlowId = flowId,
            OwnerId = userId,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        this.dbContext.WorkflowExecutions.Add(execution);
        await this.dbContext.SaveChangesAsync(cancellationToken);

        var logs = new List<object>();

        async Task LogEvent(string level, string message, object? data = null)
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                level,
                message,
                data
            };
            logs.Add(logEntry);

            await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionProgress", new
            {
                ExecutionId = resolvedExecutionId,
                FlowId = flowId,
                Log = logEntry
            }, cancellationToken);
        }

        try
        {
            await LogEvent("info", $"Starting workflow execution {resolvedExecutionId} for flow '{flowId}'");

            var nodes = await this.dbContext.AgentNodes
                .Where(n => n.FlowId == flowId && n.OwnerId == userId)
                .ToListAsync(cancellationToken);

            var edges = await this.dbContext.WorkflowEdges
                .Where(e => e.FlowId == flowId && e.OwnerId == userId)
                .ToListAsync(cancellationToken);

            if (nodes.Count == 0 && (userId != "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
            {
                nodes = await this.dbContext.AgentNodes
                    .Where(n => n.FlowId == flowId && (n.OwnerId == "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
                    .ToListAsync(cancellationToken);

                edges = await this.dbContext.WorkflowEdges
                    .Where(e => e.FlowId == flowId && (e.OwnerId == "guest-user" || WorkflowSeeder.SampleFlowIds.Contains(flowId)))
                    .ToListAsync(cancellationToken);
            }

            if (nodes.Count == 0)
            {
                await LogEvent("warning", "No nodes found for flow, executing with default agent setup.");
            }

            var agentNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Agent).ToList();
            var modelNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Model).ToList();
            var promptNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Prompt).ToList();
            var mcpNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.McpConnector).ToList();
            var skillNodes = nodes.Where(n => n.Type == (int)Protos.NodeType.Skill).ToList();

            var orderedAgents = OrderAgentsByEdges(agentNodes, edges);
            if (orderedAgents.Count == 0)
            {
                orderedAgents.Add(null);
            }

            var baseInput = string.IsNullOrWhiteSpace(inputMessage)
                ? "Initiate agent workflow and report status."
                : inputMessage;

            var currentInput = baseInput;
            var finalOutput = string.Empty;
            var stepOutputs = new List<string>();
            var lastProvider = "Ollama";
            var lastModel = "gemma3:12b";

            for (int stepIndex = 0; stepIndex < orderedAgents.Count; stepIndex++)
            {
                var currentAgentNode = orderedAgents[stepIndex];
                var agentName = FirstNonEmpty(currentAgentNode?.Name, "AgentStudioAgent");

                var modelNode = ResolveConnectedSingle(currentAgentNode, modelNodes, nodes, edges) ?? modelNodes.FirstOrDefault();
                var connectedPrompts = ResolveConnectedMany(currentAgentNode, promptNodes, nodes, edges);
                if (connectedPrompts.Count == 0 && promptNodes.Count > 0)
                {
                    connectedPrompts = promptNodes;
                }

                var connectedMcps = ResolveConnectedMany(currentAgentNode, mcpNodes, nodes, edges);
                if (connectedMcps.Count == 0 && mcpNodes.Count > 0)
                {
                    connectedMcps = mcpNodes;
                }

                var connectedSkills = ResolveConnectedMany(currentAgentNode, skillNodes, nodes, edges);
                if (connectedSkills.Count == 0 && skillNodes.Count > 0 && orderedAgents.Count <= 1)
                {
                    connectedSkills = skillNodes;
                }

                var promptParts = new List<string>();
                foreach (var pNode in connectedPrompts)
                {
                    if (!string.IsNullOrWhiteSpace(pNode.SystemPrompt))
                    {
                        promptParts.Add(pNode.SystemPrompt.Trim());
                    }
                }

                if (connectedPrompts.Count > 0)
                {
                    var promptNames = string.Join(", ", connectedPrompts.Select(p => FirstNonEmpty(p.Name, p.Id)));
                    await LogEvent("info", $"Attached System Prompt node(s): [{promptNames}]");
                }

                if (!string.IsNullOrWhiteSpace(currentAgentNode?.SystemPrompt))
                {
                    var agentPrompt = currentAgentNode.SystemPrompt.Trim();
                    if (!promptParts.Contains(agentPrompt))
                    {
                        promptParts.Add(agentPrompt);
                    }
                }

                var attachedSkillItems = new List<Protos.SkillItem>();
                var skillDirectoryPaths = new List<string>();

                foreach (var sNode in connectedSkills)
                {
                    var rawSkillRef = FirstNonEmpty(sNode.Endpoint, sNode.Name);
                    var normalizedSkillName = this.skillService.NormalizeSkillName(rawSkillRef);
                    if (string.IsNullOrWhiteSpace(normalizedSkillName))
                    {
                        continue;
                    }

                    var skillItem = await this.skillService.GetSkillByNameAsync(normalizedSkillName, cancellationToken);
                    if (skillItem == null && !string.IsNullOrWhiteSpace(sNode.SystemPrompt))
                    {
                        try
                        {
                            skillItem = await this.skillService.SaveSkillAsync(
                                normalizedSkillName,
                                FirstNonEmpty(sNode.Name, $"Skill {normalizedSkillName}"),
                                sNode.SystemPrompt,
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogWarning(ex, "Could not persist skill {SkillName} to disk; using in-memory skill definition.", normalizedSkillName);
                            skillItem = new Protos.SkillItem
                            {
                                Name = normalizedSkillName,
                                Description = FirstNonEmpty(sNode.Name, $"Skill {normalizedSkillName}"),
                                Content = sNode.SystemPrompt,
                                Path = $"skills/{normalizedSkillName}/SKILL.md"
                            };
                        }
                    }

                    if (skillItem != null && !attachedSkillItems.Any(s => s.Name.Equals(skillItem.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        attachedSkillItems.Add(skillItem);
                        var dirPath = this.skillService.GetSkillDirectoryPath(skillItem.Name);
                        if (!string.IsNullOrWhiteSpace(dirPath) && Directory.Exists(dirPath))
                        {
                            skillDirectoryPaths.Add(dirPath);
                        }
                    }
                }

                if (attachedSkillItems.Count > 0)
                {
                    var skillNamesList = string.Join(", ", attachedSkillItems.Select(s => $"{s.Name} ({s.Path})"));
                    await LogEvent("info", $"Attached Microsoft Agent Skill(s) via SKILL.md: [{skillNamesList}]");

                    var skillsSb = new StringBuilder();
                    skillsSb.AppendLine("### MICROSOFT AGENT SKILLS (SKILL.md)");
                    foreach (var skill in attachedSkillItems)
                    {
                        skillsSb.AppendLine($"#### Skill: {skill.Name}");
                        skillsSb.AppendLine($"Description: {skill.Description}");
                        skillsSb.AppendLine(skill.Content);
                        skillsSb.AppendLine();
                    }
                    promptParts.Add(skillsSb.ToString().Trim());
                }

                var systemPrompt = promptParts.Count > 0
                    ? string.Join("\n\n", promptParts)
                    : "You are an autonomous AI Agent built in AI Agent Studio.";

                systemPrompt = ApplyPromptTemplateVariables(systemPrompt, baseInput, currentInput, agentName, flowId);

                var defaultProvider = FirstNonEmpty(this.configuration["AiProviders:DefaultProvider"], "Ollama");
                var provider = FirstNonEmpty(modelNode?.ModelProvider, currentAgentNode?.ModelProvider, defaultProvider);

                var defaultModel = ResolveDefaultModelForProvider(provider);
                var modelName = FirstNonEmpty(modelNode?.ModelName, currentAgentNode?.ModelName, defaultModel);
                var endpoint = FirstNonEmpty(modelNode?.Endpoint, currentAgentNode?.Endpoint);
                var temperature = modelNode?.Temperature ?? currentAgentNode?.Temperature;
                var apiKeyOverride = FirstNonEmpty(modelNode?.McpAuthToken);

                lastProvider = provider;
                lastModel = modelName;

                await LogEvent("info", $"Resolved configuration: Provider={provider}, Model={modelName}, Agent={agentName}");

                var tools = new List<AITool>();
                if (connectedMcps.Count > 0)
                {
                    foreach (var mcpNode in connectedMcps)
                    {
                        var mcpUrl = FirstNonEmpty(mcpNode.McpServerUrl, currentAgentNode?.McpServerUrl, "builtin://tools");
                        var mcpToken = FirstNonEmpty(mcpNode.McpAuthToken, currentAgentNode?.McpAuthToken);
                        var mcpTools = (mcpNode.McpEnabledTools != null && mcpNode.McpEnabledTools.Count > 0)
                            ? mcpNode.McpEnabledTools
                            : currentAgentNode?.McpEnabledTools;

                        await LogEvent("info", $"Connecting to MCP Node '{FirstNonEmpty(mcpNode.Name, "MCP Connector")}' ({mcpUrl})...");
                        var discoveredTools = await this.mcpClientService.GetToolsAsync(
                            mcpUrl,
                            string.IsNullOrWhiteSpace(mcpToken) ? null : mcpToken,
                            mcpTools,
                            cancellationToken);

                        foreach (var dt in discoveredTools)
                        {
                            if (!tools.Any(existing => existing.Name.Equals(dt.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                tools.Add(dt);
                            }
                        }

                        AgentStudioTelemetry.RecordMcpToolsAttached(discoveredTools.Count);
                        await LogEvent(
                            "info",
                            $"Attached {discoveredTools.Count} MCP tool(s): [{string.Join(", ", discoveredTools.Select(t => t.Name))}]");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(currentAgentNode?.McpServerUrl))
                {
                    var mcpUrl = currentAgentNode.McpServerUrl;
                    var mcpToken = string.IsNullOrWhiteSpace(currentAgentNode.McpAuthToken) ? null : currentAgentNode.McpAuthToken;
                    var mcpTools = currentAgentNode.McpEnabledTools;

                    await LogEvent("info", $"Connecting to MCP Server at {mcpUrl}...");
                    var discoveredTools = await this.mcpClientService.GetToolsAsync(mcpUrl, mcpToken, mcpTools, cancellationToken);
                    tools.AddRange(discoveredTools);
                    AgentStudioTelemetry.RecordMcpToolsAttached(discoveredTools.Count);
                    await LogEvent(
                        "info",
                        $"Attached {discoveredTools.Count} MCP tool(s): [{string.Join(", ", discoveredTools.Select(t => t.Name))}]");
                }

                using var skillsProvider = skillDirectoryPaths.Count > 0
                    ? new AgentSkillsProvider(
                        skillDirectoryPaths,
                        options: new AgentSkillsProviderOptions
                        {
                            DisableLoadSkillApproval = true,
                            DisableReadSkillResourceApproval = true,
                            DisableRunSkillScriptApproval = true
                        })
                    : null;

                IChatClient chatClient = CreateChatClient(provider, modelName, endpoint, temperature, apiKeyOverride);

                await LogEvent("info", $"Executing agent reasoning loop ({agentName})...", new { Prompt = currentInput });

                string responseText;
                if (tools.Count > 0)
                {
                    if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                    {
                        await LogEvent(
                            "info",
                            $"Activating ReAct MCP tool execution loop for Gemini model '{modelName}' ({tools.Count} tools).");

                        responseText = await ExecuteReActToolLoopAsync(
                            chatClient,
                            agentName,
                            systemPrompt,
                            currentInput,
                            tools,
                            LogEvent,
                            cancellationToken);
                    }
                    else
                    {
                        try
                        {
                            var agentOptions = new ChatClientAgentOptions
                            {
                                Name = agentName,
                                ChatOptions = new ChatOptions
                                {
                                    Instructions = systemPrompt,
                                    Tools = tools
                                },
                                AIContextProviders = skillsProvider != null ? [skillsProvider] : null
                            };
                            var agentWithTools = chatClient.AsAIAgent(agentOptions);
                            var response = await agentWithTools.RunAsync(currentInput, cancellationToken: cancellationToken);
                            responseText = response.Text ?? string.Empty;
                        }
                        catch (Exception toolEx) when (
                            toolEx.Message.Contains("does not support tools", StringComparison.OrdinalIgnoreCase) ||
                            toolEx.Message.Contains("400", StringComparison.OrdinalIgnoreCase) ||
                            toolEx.Message.Contains("Bad Request", StringComparison.OrdinalIgnoreCase) ||
                            toolEx.Message.Contains("Invalid JSON payload", StringComparison.OrdinalIgnoreCase))
                        {
                            await LogEvent(
                                "info",
                                $"Model '{modelName}' rejected native OpenAI tool definitions ({toolEx.Message}); activating ReAct MCP tool execution loop ({tools.Count} tools).");

                            responseText = await ExecuteReActToolLoopAsync(
                                chatClient,
                                agentName,
                                systemPrompt,
                                currentInput,
                                tools,
                                LogEvent,
                                cancellationToken);
                        }
                    }
                }
                else
                {
                    try
                    {
                        var agentOptions = new ChatClientAgentOptions
                        {
                            Name = agentName,
                            ChatOptions = new ChatOptions
                            {
                                Instructions = systemPrompt
                            },
                            AIContextProviders = skillsProvider != null ? [skillsProvider] : null
                        };
                        var agent = chatClient.AsAIAgent(agentOptions);
                        var response = await agent.RunAsync(currentInput, cancellationToken: cancellationToken);
                        responseText = response.Text ?? string.Empty;
                    }
                    catch (Exception skillToolEx) when (
                        skillsProvider != null &&
                        (skillToolEx.Message.Contains("does not support tools", StringComparison.OrdinalIgnoreCase) ||
                         skillToolEx.Message.Contains("400", StringComparison.OrdinalIgnoreCase) ||
                         skillToolEx.Message.Contains("Bad Request", StringComparison.OrdinalIgnoreCase)))
                    {
                        var agent = chatClient.AsAIAgent(instructions: systemPrompt, name: agentName);
                        var response = await agent.RunAsync(currentInput, cancellationToken: cancellationToken);
                        responseText = response.Text ?? string.Empty;
                    }
                }

                stepOutputs.Add($"[{agentName}]\n{responseText}");
                finalOutput = orderedAgents.Count > 1
                    ? string.Join("\n\n---\n\n", stepOutputs)
                    : responseText;

                if (stepIndex + 1 < orderedAgents.Count)
                {
                    await LogEvent("info", $"Agent '{agentName}' completed step {stepIndex + 1}/{orderedAgents.Count}, passing output to next agent.");
                    currentInput = $"Previous agent ({agentName}) output:\n{responseText}\n\nOriginal task: {baseInput}";
                }
            }

            await LogEvent("info", "Workflow completed successfully.", new { Output = finalOutput });

            stopwatch.Stop();
            AgentStudioTelemetry.RecordWorkflowCompleted(flowId, lastProvider, lastModel, stopwatch.Elapsed.TotalMilliseconds);

            execution.Status = "Completed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ResultMessage = finalOutput;
            execution.LogsJson = JsonSerializer.Serialize(logs);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionFinished", new
            {
                ExecutionId = resolvedExecutionId,
                Success = true,
                Output = finalOutput
            }, cancellationToken);

            return new WorkflowExecutionResult
            {
                Success = true,
                ExecutionId = resolvedExecutionId,
                Output = finalOutput
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AgentStudioTelemetry.RecordWorkflowFailed(flowId, stopwatch.Elapsed.TotalMilliseconds);

            this.logger.LogError(ex, "Workflow execution {ExecutionId} failed", resolvedExecutionId);
            await LogEvent("error", $"Execution failed: {ex.Message}");

            execution.Status = "Failed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ResultMessage = ex.Message;
            execution.LogsJson = JsonSerializer.Serialize(logs);

            try
            {
                await this.dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                this.logger.LogWarning(dbEx, "Could not persist failed execution state for {ExecutionId}", resolvedExecutionId);
            }

            try
            {
                await this.hubContext.Clients.User(userId).SendAsync("WorkflowExecutionFinished", new
                {
                    ExecutionId = resolvedExecutionId,
                    Success = false,
                    Error = ex.Message
                }, cancellationToken);
            }
            catch (Exception hubEx)
            {
                this.logger.LogWarning(hubEx, "Could not broadcast failed execution state for {ExecutionId}", resolvedExecutionId);
            }

            return new WorkflowExecutionResult
            {
                Success = false,
                ExecutionId = resolvedExecutionId,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> ExecuteReActToolLoopAsync(
        IChatClient chatClient,
        string agentName,
        string systemPrompt,
        string userInput,
        List<AITool> tools,
        Func<string, string, object?, Task> logEvent,
        CancellationToken cancellationToken)
    {
        var toolCatalog = BuildToolCatalogDescription(tools);
        var reactSystemInstructions = new StringBuilder();
        reactSystemInstructions.AppendLine(systemPrompt);
        reactSystemInstructions.AppendLine();
        reactSystemInstructions.AppendLine("### MCP TOOLS PROTOCOL");
        reactSystemInstructions.AppendLine("CRITICAL: You ARE connected to the host environment and external tools via Model Context Protocol (MCP). NEVER claim you do not have access to system info, current time, files, or calculations.");
        reactSystemInstructions.AppendLine("Available executable MCP tools:");
        reactSystemInstructions.AppendLine(toolCatalog);
        reactSystemInstructions.AppendLine("When the user's request involves system info, time/date, math calculations, directory/file inspection, diagnostics, or web search, respond ONLY with a single line starting with TOOL_CALL: followed by a valid JSON object:");
        reactSystemInstructions.AppendLine("TOOL_CALL: {\"name\": \"<tool_name>\", \"arguments\": {\"<param_name>\": \"<value>\"}}");
        reactSystemInstructions.AppendLine("Examples:");
        reactSystemInstructions.AppendLine("TOOL_CALL: {\"name\": \"get_system_info\", \"arguments\": {\"category\": \"all\"}}");
        reactSystemInstructions.AppendLine("TOOL_CALL: {\"name\": \"get_current_time\", \"arguments\": {\"timezone\": \"UTC\"}}");
        reactSystemInstructions.AppendLine("TOOL_CALL: {\"name\": \"calculate_expression\", \"arguments\": {\"expression\": \"(12 * 5) + 10\"}}");
        reactSystemInstructions.AppendLine("If the user's request is a simple greeting or general question that does not use any attached MCP tool, answer directly without emitting TOOL_CALL.");

        var currentPrompt = userInput;
        var maxIterations = 4;
        var invokedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invokedCallSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolResultsHistory = new List<string>();

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            string activeInstructions;
            if (iteration == 0)
            {
                activeInstructions = reactSystemInstructions.ToString();
            }
            else
            {
                var remainingTools = tools.Where(t => !invokedTools.Contains(t.Name)).ToList();
                var synthSb = new StringBuilder();
                synthSb.AppendLine(systemPrompt);
                synthSb.AppendLine();
                synthSb.AppendLine("### VERIFIED MCP TOOL RESULTS");
                foreach (var item in toolResultsHistory)
                {
                    synthSb.AppendLine(item);
                }
                synthSb.AppendLine();
                if (remainingTools.Count > 0)
                {
                    synthSb.AppendLine("Additional MCP tools available if the user also requested them:");
                    synthSb.AppendLine(BuildToolCatalogDescription(remainingTools));
                    synthSb.AppendLine("If the user's request ALSO requires one of the additional tools above that has NOT been executed yet, respond ONLY with: TOOL_CALL: {\"name\": \"<tool_name>\", \"arguments\": {...}}");
                }
                synthSb.AppendLine("Otherwise, provide a direct, confident, and complete final answer to the user's request using the EXACT numbers, versions, dates, and facts from VERIFIED MCP TOOL RESULTS above combined with your own knowledge. NEVER say 'wyniki wyszukiwania nie zawierają' or 'nie mogę podać' — always synthesize the best, most informative answer possible.");
                activeInstructions = synthSb.ToString();
            }

            var agent = chatClient.AsAIAgent(instructions: activeInstructions, name: agentName);
            var response = await agent.RunAsync(currentPrompt, cancellationToken: cancellationToken);
            var text = (response.Text ?? string.Empty).Trim();

            var parsed = TryParseToolCall(text, tools, out var toolName, out var arguments);
            if (!parsed && iteration == 0)
            {
                parsed = TryDetectImplicitToolCall(userInput, text, tools, invokedTools, out toolName, out arguments);
            }

            if (!parsed)
            {
                return text;
            }

            if (arguments.Count == 0 ||
                arguments.Values.All(v => string.IsNullOrWhiteSpace(v?.ToString()) || v.ToString()!.Contains('<')))
            {
                PopulateDefaultArgumentsForImplicitTool(toolName, userInput, arguments);
            }

            var callSignature = $"{toolName}:{JsonSerializer.Serialize(arguments)}";
            if (!invokedCallSignatures.Add(callSignature))
            {
                break;
            }

            invokedTools.Add(toolName);

            var matchedTool = tools.OfType<AIFunction>()
                .FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

            if (matchedTool == null)
            {
                return text;
            }

            await logEvent("info", $"[MCP Tool Call] Invoking '{matchedTool.Name}' with arguments: {JsonSerializer.Serialize(arguments)}", new
            {
                Tool = matchedTool.Name,
                Arguments = arguments
            });

            var toolOutput = await InvokeAiFunctionSafeAsync(matchedTool, arguments, cancellationToken);

            await logEvent("info", $"[MCP Tool Result] '{matchedTool.Name}' -> {toolOutput}", new
            {
                Tool = matchedTool.Name,
                Output = toolOutput
            });

            toolResultsHistory.Add($"- Tool '{matchedTool.Name}' returned: {toolOutput}");

            currentPrompt =
                $"Verified MCP Tool Results:\n{string.Join("\n", toolResultsHistory)}\n\n" +
                $"Original user request: {userInput}\n\n" +
                "Provide the final answer based on the Verified MCP Tool Results above (combining with your own knowledge if needed for general questions, or emit TOOL_CALL only if another uncalled tool is still needed).";
        }

        var finalInstructions =
            $"{systemPrompt}\n\n" +
            $"Verified MCP Tool Results:\n{string.Join("\n", toolResultsHistory)}\n\n" +
            "Provide the final answer to the user using the exact values from Verified MCP Tool Results above (and supplement with your own knowledge if needed).";
        var fallbackFinalAgent = chatClient.AsAIAgent(instructions: finalInstructions, name: agentName);
        var fallbackFinalResponse = await fallbackFinalAgent.RunAsync(currentPrompt, cancellationToken: cancellationToken);
        return (fallbackFinalResponse.Text ?? string.Empty).Trim();
    }

    private static string BuildToolCatalogDescription(List<AITool> tools)
    {
        var sb = new StringBuilder();
        foreach (var tool in tools)
        {
            if (tool is AIFunction fn)
            {
                var paramNames = GetFunctionParameterNames(fn);
                var paramSignature = paramNames.Count > 0 ? string.Join(", ", paramNames) : "";
                var desc = string.IsNullOrWhiteSpace(fn.Description) ? "MCP tool" : fn.Description;
                sb.AppendLine($"- {fn.Name}({paramSignature}): {desc}");
            }
            else
            {
                sb.AppendLine($"- {tool.Name}");
            }
        }
        return sb.ToString();
    }

    private static List<string> GetFunctionParameterNames(AIFunction fn)
    {
        var names = new List<string>();
        try
        {
            if (fn.JsonSchema.ValueKind == JsonValueKind.Object &&
                fn.JsonSchema.TryGetProperty("properties", out var props) &&
                props.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in props.EnumerateObject())
                {
                    names.Add(prop.Name);
                }
            }
        }
        catch
        {
            // Ignore schema inspection errors
        }
        return names;
    }

    private static bool TryParseToolCall(
        string responseText,
        List<AITool> availableTools,
        out string toolName,
        out Dictionary<string, object?> arguments)
    {
        toolName = string.Empty;
        arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var jsonCandidates = new List<string>();

        var markerIdx = responseText.IndexOf("TOOL_CALL:", StringComparison.OrdinalIgnoreCase);
        if (markerIdx >= 0)
        {
            var afterMarker = responseText[(markerIdx + "TOOL_CALL:".Length)..].Trim();
            var extractedJson = ExtractBalancedJsonObject(afterMarker);
            if (!string.IsNullOrWhiteSpace(extractedJson))
            {
                jsonCandidates.Add(extractedJson);
            }
            else
            {
                // Support plain function call syntax e.g. TOOL_CALL: get_system_info()
                foreach (var t in availableTools)
                {
                    if (afterMarker.StartsWith(t.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        toolName = t.Name;
                        return true;
                    }
                }
            }
        }

        var codeBlockMatch = Regex.Match(responseText, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (codeBlockMatch.Success)
        {
            jsonCandidates.Add(codeBlockMatch.Groups[1].Value);
        }

        var balancedFromFull = ExtractBalancedJsonObject(responseText);
        if (!string.IsNullOrWhiteSpace(balancedFromFull))
        {
            jsonCandidates.Add(balancedFromFull);
        }

        foreach (var candidate in jsonCandidates)
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? parsedName = null;
                if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    parsedName = nameProp.GetString();
                }
                else if (root.TryGetProperty("tool", out var toolProp) && toolProp.ValueKind == JsonValueKind.String)
                {
                    parsedName = toolProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(parsedName))
                {
                    continue;
                }

                if (!availableTools.Any(t => t.Name.Equals(parsedName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                toolName = parsedName;

                if (root.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in argsProp.EnumerateObject())
                    {
                        arguments[p.Name] = ConvertJsonElement(p.Value);
                    }
                }
                else if (root.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in paramsProp.EnumerateObject())
                    {
                        arguments[p.Name] = ConvertJsonElement(p.Value);
                    }
                }
                else
                {
                    foreach (var p in root.EnumerateObject())
                    {
                        if (!p.Name.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                            !p.Name.Equals("tool", StringComparison.OrdinalIgnoreCase))
                        {
                            arguments[p.Name] = ConvertJsonElement(p.Value);
                        }
                    }
                }

                return true;
            }
            catch
            {
                // Ignore malformed JSON candidate
            }
        }

        return false;
    }

    private static string? ExtractBalancedJsonObject(string input)
    {
        var start = input.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (int i = start; i < input.Length; i++)
        {
            var ch = input[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return input[start..(i + 1)];
                    }
                }
            }
        }

        return null;
    }

    private static bool TryDetectImplicitToolCall(
        string userInput,
        string modelResponseText,
        List<AITool> availableTools,
        HashSet<string> alreadyInvoked,
        out string toolName,
        out Dictionary<string, object?> arguments)
    {
        toolName = string.Empty;
        arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userInput))
        {
            return false;
        }

        var lowerInput = userInput.ToLowerInvariant();
        var lowerResponse = (modelResponseText ?? string.Empty).ToLowerInvariant();

        bool HasTool(string name) =>
            !alreadyInvoked.Contains(name) &&
            availableTools.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        // 1. Explicit tool name mentioned in user prompt or model output
        foreach (var t in availableTools)
        {
            if (alreadyInvoked.Contains(t.Name))
            {
                continue;
            }

            if (lowerInput.Contains(t.Name.ToLowerInvariant()) || lowerResponse.Contains(t.Name.ToLowerInvariant()))
            {
                toolName = t.Name;
                PopulateDefaultArgumentsForImplicitTool(toolName, userInput, arguments);
                return true;
            }
        }

        // 2. Intent matching for get_system_info
        if (HasTool("get_system_info") &&
            (lowerInput.Contains("info o systemie") ||
             lowerInput.Contains("informacje o systemie") ||
             lowerInput.Contains("system info") ||
             lowerInput.Contains("diagnostyk") ||
             lowerInput.Contains("procesor") ||
             lowerInput.Contains("rdzeni") ||
             lowerInput.Contains("wersję .net") ||
             lowerInput.Contains("wersje .net") ||
             lowerInput.Contains("środowisk") ||
             lowerInput.Contains("srodowisk") ||
             lowerInput.Contains("system operacyjny") ||
             lowerInput.Contains("cpu") ||
             lowerInput.Contains("os ")))
        {
            toolName = "get_system_info";
            arguments["category"] = "all";
            return true;
        }

        // 3. Intent matching for get_current_time
        if (HasTool("get_current_time") &&
            (lowerInput.Contains("aktualny czas") ||
             lowerInput.Contains("jaki jest czas") ||
             lowerInput.Contains("która godzina") ||
             lowerInput.Contains("ktora godzina") ||
             lowerInput.Contains("czas utc") ||
             lowerInput.Contains("dzisiejsza data") ||
             lowerInput.Contains("current time")))
        {
            toolName = "get_current_time";
            arguments["timezone"] = "UTC";
            return true;
        }

        // 4. Intent matching for calculate_expression
        if (HasTool("calculate_expression") &&
            (lowerInput.Contains("oblicz") || lowerInput.Contains("policz") || lowerInput.Contains("ile to")) &&
            Regex.IsMatch(userInput, @"\d+\s*[\+\-\*\/]\s*\d+"))
        {
            toolName = "calculate_expression";
            PopulateDefaultArgumentsForImplicitTool(toolName, userInput, arguments);
            return true;
        }

        // 5. Intent matching for list_dir
        if (HasTool("list_dir") &&
            (lowerInput.Contains("zawartość katalogu") ||
             lowerInput.Contains("wypisz pliki") ||
             lowerInput.Contains("lista plików") ||
             lowerInput.Contains("list files") ||
             lowerInput.Contains("katalogu roboczego")))
        {
            toolName = "list_dir";
            arguments["path"] = ".";
            return true;
        }

        // 6. Intent matching for search_web (live web/version/release queries or when model claims lack of current info)
        if (HasTool("search_web") &&
            (lowerInput.Contains("najnowsz") ||
             lowerInput.Contains("wersj") ||
             lowerInput.Contains("wyszukaj") ||
             lowerInput.Contains("znajdź") ||
             lowerInput.Contains("znajdz") ||
             lowerInput.Contains("latest") ||
             lowerInput.Contains("release") ||
             lowerResponse.Contains("nie mam dostępu") ||
             lowerResponse.Contains("nie mogę podać") ||
             lowerResponse.Contains("nie posiadam") ||
             lowerResponse.Contains("brak dostępu")))
        {
            toolName = "search_web";
            arguments["query"] = userInput;
            return true;
        }

        return false;
    }

    private static void PopulateDefaultArgumentsForImplicitTool(
        string toolName,
        string userInput,
        Dictionary<string, object?> arguments)
    {
        if (toolName.Equals("calculate_expression", StringComparison.OrdinalIgnoreCase))
        {
            var mathMatch = Regex.Match(userInput, @"[\(\d][\d\s\+\-\*\/\.\,\(\)]+[\d\)]");
            arguments["expression"] = mathMatch.Success ? mathMatch.Value.Trim() : userInput;
        }
        else if (toolName.Equals("get_system_info", StringComparison.OrdinalIgnoreCase))
        {
            arguments["category"] = "all";
        }
        else if (toolName.Equals("get_current_time", StringComparison.OrdinalIgnoreCase))
        {
            arguments["timezone"] = "UTC";
        }
        else if (toolName.Equals("list_dir", StringComparison.OrdinalIgnoreCase))
        {
            arguments["path"] = ".";
        }
        else if (toolName.Equals("search_web", StringComparison.OrdinalIgnoreCase) ||
                 toolName.Equals("execute_query", StringComparison.OrdinalIgnoreCase))
        {
            arguments["query"] = userInput;
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l.ToString() : element.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static async Task<string> InvokeAiFunctionSafeAsync(
        AIFunction fn,
        Dictionary<string, object?> rawArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            var expectedParams = GetFunctionParameterNames(fn);
            var aiArgs = new AIFunctionArguments();

            foreach (var kv in rawArgs)
            {
                aiArgs[kv.Key] = kv.Value;
            }

            if (expectedParams.Count > 0 && rawArgs.Count > 0)
            {
                var firstExpected = expectedParams[0];
                if (!aiArgs.ContainsKey(firstExpected))
                {
                    aiArgs[firstExpected] = rawArgs.Values.First();
                }
            }
            else if (expectedParams.Count == 1 && rawArgs.Count == 0)
            {
                aiArgs[expectedParams[0]] = string.Empty;
            }

            var result = await fn.InvokeAsync(aiArgs, cancellationToken);
            return result?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            return $"Tool execution error ({fn.Name}): {ex.Message}";
        }
    }

    private static string ApplyPromptTemplateVariables(
        string prompt,
        string baseInput,
        string currentInput,
        string agentName,
        string flowId)
    {
        return prompt
            .Replace("{{input}}", baseInput, StringComparison.OrdinalIgnoreCase)
            .Replace("{{query}}", baseInput, StringComparison.OrdinalIgnoreCase)
            .Replace("{{previous_output}}", currentInput, StringComparison.OrdinalIgnoreCase)
            .Replace("{{date}}", DateTime.UtcNow.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{time}}", DateTime.UtcNow.ToString("HH:mm:ss 'UTC'"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{agent}}", agentName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{flowId}}", flowId, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveDefaultModelForProvider(string provider)
    {
        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(this.configuration["AiProviders:Ollama:DefaultModel"], "gemma3:12b");
        }

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(this.configuration["AiProviders:Gemini:DefaultModel"], "gemini-3.5-flash-lite");
        }

        return FirstNonEmpty(this.configuration["AiProviders:OpenAI:DefaultModel"], "gpt-4o");
    }

    public IChatClient CreateChatClient(
        string provider,
        string modelName,
        string? customEndpoint,
        double? temperature,
        string? apiKeyOverride = null)
    {
        IChatClient baseClient;

        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            var ollamaEndpoint = FirstNonEmpty(
                customEndpoint,
                this.configuration["AiProviders:Ollama:Endpoint"],
                "http://localhost:11434");

            var trimmed = ollamaEndpoint.TrimEnd('/');
            if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                trimmed += "/v1";
            }

            var baseUri = new Uri(trimmed);
            var openAiOptions = new OpenAIClientOptions { Endpoint = baseUri };
            var openAiClient = new OpenAIClient(new ApiKeyCredential("ollama"), openAiOptions);
            baseClient = openAiClient.GetChatClient(modelName).AsIChatClient();
        }
        else if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var geminiApiKey = FirstNonEmpty(
                apiKeyOverride,
                this.configuration["AiProviders:Gemini:ApiKey"],
                Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
                Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));

            if (string.IsNullOrWhiteSpace(geminiApiKey))
            {
                throw new InvalidOperationException(
                    "Brak klucza API Google Gemini. Wklej swój klucz API w prawym panelu Inspector dla węzła Model (pole API Key), ustaw zmienną środowiskową GEMINI_API_KEY lub skonfiguruj AiProviders:Gemini:ApiKey w appsettings.json.");
            }

            var geminiEndpoint = FirstNonEmpty(
                customEndpoint,
                this.configuration["AiProviders:Gemini:Endpoint"],
                "https://generativelanguage.googleapis.com/v1beta/openai/");

            if (geminiEndpoint.Contains("localhost:11434", StringComparison.OrdinalIgnoreCase))
            {
                geminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/";
            }

            if (!geminiEndpoint.EndsWith("/"))
            {
                geminiEndpoint += "/";
            }

            var baseUri = new Uri(geminiEndpoint);
            var openAiOptions = new OpenAIClientOptions { Endpoint = baseUri };
            var openAiClient = new OpenAIClient(new ApiKeyCredential(geminiApiKey), openAiOptions);
            baseClient = openAiClient.GetChatClient(modelName).AsIChatClient();
        }
        else
        {
            var apiKey = FirstNonEmpty(
                apiKeyOverride,
                this.configuration["AiProviders:OpenAI:ApiKey"],
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                "placeholder-key");

            var defaultEndpoint = this.configuration["AiProviders:OpenAI:Endpoint"];

            OpenAIClientOptions? openAiOptions = null;
            if (!string.IsNullOrWhiteSpace(customEndpoint) && Uri.TryCreate(customEndpoint, UriKind.Absolute, out var customUri))
            {
                openAiOptions = new OpenAIClientOptions { Endpoint = customUri };
            }
            else if (!string.IsNullOrWhiteSpace(defaultEndpoint) && Uri.TryCreate(defaultEndpoint, UriKind.Absolute, out var uri))
            {
                openAiOptions = new OpenAIClientOptions { Endpoint = uri };
            }

            var openAiClient = openAiOptions != null
                ? new OpenAIClient(new ApiKeyCredential(apiKey), openAiOptions)
                : new OpenAIClient(new ApiKeyCredential(apiKey));

            baseClient = openAiClient.GetChatClient(modelName).AsIChatClient();
        }

        if (temperature.HasValue)
        {
            var tempValue = (float)temperature.Value;
            return baseClient
                .AsBuilder()
                .ConfigureOptions(options => options.Temperature = tempValue)
                .Build();
        }

        return baseClient;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var val in values)
        {
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val.Trim();
            }
        }
        return string.Empty;
    }

    private static bool AreConnected(string nodeIdA, string nodeIdB, List<WorkflowEdgeEntity> edges)
    {
        return edges.Any(e =>
            (e.SourceNodeId == nodeIdA && e.TargetNodeId == nodeIdB) ||
            (e.SourceNodeId == nodeIdB && e.TargetNodeId == nodeIdA));
    }

    private static AgentNodeEntity? ResolveConnectedSingle(
        AgentNodeEntity? agentNode,
        List<AgentNodeEntity> candidates,
        List<AgentNodeEntity> allNodes,
        List<WorkflowEdgeEntity> edges)
    {
        return ResolveConnectedMany(agentNode, candidates, allNodes, edges).FirstOrDefault();
    }

    private static List<AgentNodeEntity> ResolveConnectedMany(
        AgentNodeEntity? agentNode,
        List<AgentNodeEntity> candidates,
        List<AgentNodeEntity> allNodes,
        List<WorkflowEdgeEntity> edges)
    {
        if (agentNode == null || candidates.Count == 0 || edges.Count == 0)
        {
            return new List<AgentNodeEntity>();
        }

        // Direct 1-hop connection to agentNode
        var direct = candidates.Where(c => AreConnected(agentNode.Id, c.Id, edges)).ToList();

        // Also support 2-hop connections through non-agent nodes (e.g. Prompt -> Model -> Agent)
        var nonAgentNodeIds = allNodes
            .Where(n => n.Type != (int)Protos.NodeType.Agent)
            .Select(n => n.Id)
            .ToHashSet();

        var intermediateIds = nonAgentNodeIds
            .Where(id => AreConnected(agentNode.Id, id, edges))
            .ToList();

        foreach (var candidate in candidates)
        {
            if (!direct.Contains(candidate) &&
                intermediateIds.Any(interId => AreConnected(interId, candidate.Id, edges)))
            {
                direct.Add(candidate);
            }
        }

        return direct;
    }

    private static List<AgentNodeEntity?> OrderAgentsByEdges(
        List<AgentNodeEntity> agentNodes,
        List<WorkflowEdgeEntity> edges)
    {
        if (agentNodes.Count <= 1)
        {
            return agentNodes.Cast<AgentNodeEntity?>().ToList();
        }

        var agentIds = new HashSet<string>(agentNodes.Select(a => a.Id));
        var agentEdges = edges
            .Where(e => agentIds.Contains(e.SourceNodeId) && agentIds.Contains(e.TargetNodeId) && e.SourceNodeId != e.TargetNodeId)
            .ToList();

        if (agentEdges.Count == 0)
        {
            return new List<AgentNodeEntity?> { agentNodes.First() };
        }

        var inDegree = agentNodes.ToDictionary(a => a.Id, _ => 0);
        var adjacency = agentNodes.ToDictionary(a => a.Id, _ => new List<string>());

        foreach (var edge in agentEdges)
        {
            adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
            inDegree[edge.TargetNodeId]++;
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var orderedIds = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            orderedIds.Add(current);
            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (orderedIds.Count == 0)
        {
            return new List<AgentNodeEntity?> { agentNodes.First() };
        }

        var byId = agentNodes.ToDictionary(a => a.Id);
        return orderedIds.Select(id => (AgentNodeEntity?)byId[id]).ToList();
    }
}
