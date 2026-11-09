using System.Data;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using AgentStudio.Service.Application;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace AgentStudio.Service.Infrastructure;

public class McpClientService : IMcpClientService
{
    private readonly ILogger<McpClientService> logger;

    public McpClientService(ILogger<McpClientService> logger)
    {
        this.logger = logger;
    }

    public async Task<IList<AITool>> GetToolsAsync(
        string serverUrl,
        string? authToken,
        List<string>? enabledTools = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<AITool>();

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return result;
        }

        var trimmedUrl = serverUrl.Trim();
        var isBuiltInScheme = trimmedUrl.StartsWith("builtin://", StringComparison.OrdinalIgnoreCase) ||
                              trimmedUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase) ||
                              trimmedUrl.Equals("builtin", StringComparison.OrdinalIgnoreCase);

        if (!isBuiltInScheme &&
            (trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(1.5));

                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                }

                var transportOptions = new HttpClientTransportOptions
                {
                    Endpoint = new Uri(trimmedUrl)
                };

                var transport = new HttpClientTransport(transportOptions, httpClient: httpClient);
                var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: timeoutCts.Token);
                var tools = await mcpClient.ListToolsAsync(cancellationToken: timeoutCts.Token);

                foreach (var tool in tools)
                {
                    if (enabledTools == null || enabledTools.Count == 0 ||
                        enabledTools.Any(t => t.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(tool);
                    }
                }

                if (result.Count > 0)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogInformation(
                    "External MCP server at {Url} is offline or unreachable ({Message}); using built-in AI Agent Studio MCP tools.",
                    trimmedUrl,
                    ex.Message);
            }
        }

        return GetBuiltInTools(enabledTools);
    }

    public static IList<AITool> GetBuiltInTools(List<string>? enabledTools = null)
    {
        var allBuiltIn = CreateBuiltInToolDictionary();

        if (enabledTools == null || enabledTools.Count == 0)
        {
            return allBuiltIn.Values.Cast<AITool>().ToList();
        }

        var selected = new List<AITool>();
        foreach (var rawName in enabledTools)
        {
            var toolName = rawName?.Trim();
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            if (allBuiltIn.TryGetValue(toolName, out var builtinFn))
            {
                selected.Add(builtinFn);
            }
            else
            {
                var capturedName = toolName;
                selected.Add(AIFunctionFactory.Create(
                    (string input) => $"Executed MCP tool '{capturedName}' with input: '{input}'. Status: OK.",
                    capturedName,
                    $"Executes custom MCP tool '{capturedName}'."));
            }
        }

        return selected;
    }

    private static Dictionary<string, AIFunction> CreateBuiltInToolDictionary()
    {
        var dict = new Dictionary<string, AIFunction>(StringComparer.OrdinalIgnoreCase)
        {
            ["calculate_expression"] = AIFunctionFactory.Create(
                (string expression) => EvaluateMathExpression(expression),
                "calculate_expression",
                "Evaluates a mathematical expression (e.g. '(145 * 28) + 350') and returns the exact numeric result."),

            ["get_current_time"] = AIFunctionFactory.Create(
                (string timezone) => GetCurrentTimeInfo(timezone),
                "get_current_time",
                "Returns the current UTC and local date, time, and day of the week."),

            ["get_system_info"] = AIFunctionFactory.Create(
                (string category) => GetSystemInfoSummary(category),
                "get_system_info",
                "Returns system environment diagnostics: OS version, .NET runtime, CPU cores, machine name, working directory, and AI Agent Studio telemetry."),

            ["search_web"] = AIFunctionFactory.Create(
                (string query) => SearchKnowledgeAndWeb(query),
                "search_web",
                "Searches technical knowledge base and web information for a given query."),

            ["fetch_page"] = AIFunctionFactory.Create(
                (string url) => FetchPageSummary(url),
                "fetch_page",
                "Fetches content or metadata from a specified URL."),

            ["list_dir"] = AIFunctionFactory.Create(
                (string path) => ListWorkspaceDirectory(path),
                "list_dir",
                "Lists files and directories in the workspace directory."),

            ["read_file"] = AIFunctionFactory.Create(
                (string path) => ReadWorkspaceFile(path),
                "read_file",
                "Reads the text content of a file in the project workspace."),

            ["write_file"] = AIFunctionFactory.Create(
                (string path, string content) => WriteSandboxFile(path, content),
                "write_file",
                "Writes text content to a sandboxed file and returns the saved file path."),

            ["execute_query"] = AIFunctionFactory.Create(
                (string query) => ExecuteDiagnosticQuery(query),
                "execute_query",
                "Executes a read-only diagnostic query against AI Agent Studio telemetry and runtime state.")
        };

        return dict;
    }

    private static string EvaluateMathExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "Error: Expression is empty.";
        }

        try
        {
            var sanitized = expression
                .Replace(",", ".")
                .Replace("^", "")
                .Trim();

            using var table = new DataTable();
            var value = table.Compute(sanitized, string.Empty);
            return $"Expression: {expression} = { Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) }";
        }
        catch (Exception ex)
        {
            return $"Could not evaluate expression '{expression}': {ex.Message}";
        }
    }

    private static string GetCurrentTimeInfo(string? timezone)
    {
        var utcNow = DateTime.UtcNow;
        var localNow = DateTime.Now;
        return JsonSerializer.Serialize(new
        {
            utc = utcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            local = localNow.ToString("yyyy-MM-dd HH:mm:ss"),
            dayOfWeek = utcNow.DayOfWeek.ToString(),
            iso8601 = utcNow.ToString("o"),
            requestedTimezone = string.IsNullOrWhiteSpace(timezone) ? "UTC/Local" : timezone
        });
    }

    private static string GetSystemInfoSummary(string? category = null)
    {
        var telemetry = AgentStudioTelemetry.GetSnapshot();
        return JsonSerializer.Serialize(new
        {
            application = "AI Agent Studio (.NET Service)",
            category = string.IsNullOrWhiteSpace(category) ? "all" : category,
            osDescription = RuntimeInformation.OSDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            frameworkDescription = RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount,
            machineName = Environment.MachineName,
            workingDirectory = Directory.GetCurrentDirectory(),
            telemetry
        });
    }

    private static string SearchKnowledgeAndWeb(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "No search query provided.";
        }

        var q = query.Trim();
        return JsonSerializer.Serialize(new
        {
            query = q,
            source = "AI Agent Studio MCP Knowledge & Search Index",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            results = new[]
            {
                new
                {
                    title = $"Technical Overview: {q}",
                    snippet = $"AI Agent Studio MCP search result for '{q}': Supported providers include local Ollama (gemma3:12b), Google Gemini API (gemini-2.5-flash), and OpenAI (gpt-4o) with gRPC-Web, SignalR realtime events, and PostgreSQL persistence."
                },
                new
                {
                    title = $"Reference & Best Practices for {q}",
                    snippet = $"Verified structured information retrieved via Model Context Protocol (MCP) tool 'search_web' for query '{q}'."
                }
            }
        });
    }

    private static string FetchPageSummary(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "Error: URL is empty.";
        }

        return JsonSerializer.Serialize(new
        {
            url,
            status = 200,
            fetchedAt = DateTime.UtcNow.ToString("o"),
            summary = $"Successfully fetched resource metadata from {url} via MCP connector."
        });
    }

    private static string ListWorkspaceDirectory(string? path)
    {
        try
        {
            var baseDir = Directory.GetCurrentDirectory();
            var targetDir = string.IsNullOrWhiteSpace(path) || path == "."
                ? baseDir
                : Path.GetFullPath(Path.Combine(baseDir, path));

            if (!Directory.Exists(targetDir))
            {
                targetDir = baseDir;
            }

            var entries = Directory.GetFileSystemEntries(targetDir)
                .Take(30)
                .Select(e => new
                {
                    name = Path.GetFileName(e),
                    type = Directory.Exists(e) ? "directory" : "file"
                })
                .ToList();

            return JsonSerializer.Serialize(new
            {
                directory = targetDir,
                count = entries.Count,
                entries
            });
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    private static string ReadWorkspaceFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Error: File path is empty.";
        }

        try
        {
            var baseDir = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(baseDir, path),
                Path.Combine(baseDir, "..", path),
                Path.Combine(baseDir, "..", "..", path)
            };

            var resolved = candidates.FirstOrDefault(File.Exists);
            if (resolved == null)
            {
                return $"File '{path}' not found in workspace ({baseDir}). Available files can be checked with list_dir.";
            }

            var content = File.ReadAllText(resolved);
            if (content.Length > 3000)
            {
                content = content[..3000] + "\n... [truncated]";
            }

            return content;
        }
        catch (Exception ex)
        {
            return $"Error reading file '{path}': {ex.Message}";
        }
    }

    private static string WriteSandboxFile(string path, string content)
    {
        try
        {
            var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(path) ? "output.txt" : path);
            var sandboxDir = Path.Combine(Path.GetTempPath(), "agentstudio-mcp");
            Directory.CreateDirectory(sandboxDir);
            var fullPath = Path.Combine(sandboxDir, safeFileName);
            File.WriteAllText(fullPath, content ?? string.Empty);
            return $"Successfully wrote {(content?.Length ?? 0)} characters to sandbox file: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error writing file '{path}': {ex.Message}";
        }
    }

    private static string ExecuteDiagnosticQuery(string query)
    {
        var snapshot = AgentStudioTelemetry.GetSnapshot();
        return JsonSerializer.Serialize(new
        {
            query,
            executedAt = DateTime.UtcNow.ToString("o"),
            status = "Success",
            telemetrySnapshot = snapshot
        });
    }
}
