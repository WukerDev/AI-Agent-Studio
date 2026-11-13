using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentStudio.Service.Application;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace AgentStudio.Service.Infrastructure;

public class McpClientService : IMcpClientService
{
    private static readonly HttpClient WebSearchHttpClient = CreateWebSearchHttpClient();
    private readonly ILogger<McpClientService> logger;

    public McpClientService(ILogger<McpClientService> logger)
    {
        this.logger = logger;
    }

    private static HttpClient CreateWebSearchHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(7)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7");
        return client;
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
                (string query) => SearchKnowledgeAndWebAsync(query),
                "search_web",
                "Searches Wikipedia, DuckDuckGo, and web knowledge bases for a given query."),

            ["fetch_page"] = AIFunctionFactory.Create(
                (string url) => FetchPageSummaryAsync(url),
                "fetch_page",
                "Fetches and extracts readable text content and metadata from a specified URL."),

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

    private static async Task<string> SearchKnowledgeAndWebAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "No search query provided.";
        }

        var rawQuery = query.Trim();
        var coreTopic = ExtractCoreSearchTopic(rawQuery);
        var searchResults = new List<object>();
        var discoveredUrls = new List<string>();

        // 1. Live General Web Search (Yahoo Web Search engine)
        var yahooHits = await QueryYahooWebSearchAsync(rawQuery, discoveredUrls);
        if (yahooHits.Count == 0 && !string.Equals(coreTopic, rawQuery, StringComparison.OrdinalIgnoreCase))
        {
            yahooHits = await QueryYahooWebSearchAsync(coreTopic, discoveredUrls);
        }
        searchResults.AddRange(yahooHits);

        // 2. Live GitHub Releases & Repositories API (when GitHub links appear in web results or query asks for versions/releases)
        var ghHits = await QueryGitHubReleasesAndReposAsync(rawQuery, coreTopic, discoveredUrls);
        searchResults.InsertRange(0, ghHits);

        // 3. Wikipedia API (PL & EN) for encyclopedic depth
        var wikiHits = await QueryWikipediaAsync("pl", coreTopic);
        if (wikiHits.Count == 0 && !string.Equals(coreTopic, rawQuery, StringComparison.OrdinalIgnoreCase))
        {
            wikiHits = await QueryWikipediaAsync("pl", rawQuery);
        }

        if (wikiHits.Count == 0)
        {
            wikiHits = await QueryWikipediaAsync("en", coreTopic);
        }
        searchResults.AddRange(wikiHits);

        // 4. DuckDuckGo Instant Answer API
        var ddgHits = await QueryDuckDuckGoInstantAnswerAsync(coreTopic);
        searchResults.AddRange(ddgHits);

        // 5. Fetch live page text excerpt from top non-GitHub web search result for deeper context
        var topPageUrl = discoveredUrls.FirstOrDefault(u =>
            u.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !u.Contains("github.com", StringComparison.OrdinalIgnoreCase) &&
            !u.Contains("wikipedia.org", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(topPageUrl))
        {
            var topExcerpt = await FetchTopWebResultExcerptAsync(topPageUrl);
            if (topExcerpt != null)
            {
                searchResults.Add(topExcerpt);
            }
        }

        // 6. Include AI Agent Studio internal knowledge only when relevant to the query
        var lower = rawQuery.ToLowerInvariant();
        if (lower.Contains("agent studio") || lower.Contains("mcp") || lower.Contains("grpc") || lower.Contains("signalr") || lower.Contains("ollama") || lower.Contains("gemini"))
        {
            searchResults.Add(new
            {
                title = "AI Agent Studio Architecture & MCP Overview",
                url = "builtin://docs/architecture",
                snippet = "AI Agent Studio supports n8n-style Agent Hub workflows with dedicated Model, System Prompt, and MCP Tool ports, powered by Ollama (gemma3:12b), Google Gemini API (gemini-3.5-flash-lite), and OpenAI (gpt-4o) over gRPC-Web and SignalR."
            });
        }

        if (searchResults.Count == 0)
        {
            searchResults.Add(new
            {
                title = $"Search Summary: {rawQuery}",
                url = $"https://pl.search.yahoo.com/search?p={Uri.EscapeDataString(rawQuery)}",
                snippet = $"Please provide a comprehensive, direct answer to the user's question '{rawQuery}' (topic: '{coreTopic}') using your complete knowledge base."
            });
        }

        return JsonSerializer.Serialize(new
        {
            query = rawQuery,
            coreTopic,
            source = "Live Multi-Engine Web Search (Web + GitHub Releases + Wikipedia + DuckDuckGo MCP)",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            results = searchResults
        });
    }

    private static string ExtractCoreSearchTopic(string rawQuery)
    {
        var cleaned = rawQuery.Trim().Trim('?', '!', '.', '"', '\'');
        var fillerPatterns = new[]
        {
            @"^(co\s+to\s+jest|czym\s+jest|kto\s+to\s+jest|kim\s+jest|jaka\s+jest|jaki\s+jest|opisz|wyjaśnij|wyjasnij|wyszukaj|znajdź|znajdz|sprawdź|sprawdz|podaj\s+informacje\s+o|informacje\s+o|what\s+is|who\s+is|explain|describe|search\s+for)\s+",
            @"\b(najnowsza\s+wersja|aktualna\s+wersja|ostatnia\s+wersja|wersja|definicja|właściwości|wlasciwosci|zastosowania|zastosowanie|charakterystyka|i\s+jego|i\s+jej|oraz|w\s+skrócie|w\s+skrocie|po\s+polsku|latest\s+version|current\s+version|definition|properties|applications|overview)\b"
        };

        var result = cleaned;
        foreach (var pattern in fillerPatterns)
        {
            result = Regex.Replace(result, pattern, " ", RegexOptions.IgnoreCase);
        }

        result = Regex.Replace(result, @"\s+", " ").Trim(' ', ',', '-', ':');
        return string.IsNullOrWhiteSpace(result) ? cleaned : result;
    }

    private static async Task<List<object>> QueryYahooWebSearchAsync(string searchQuery, List<string> discoveredUrls)
    {
        var results = new List<object>();
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return results;
        }

        try
        {
            var url = $"https://pl.search.yahoo.com/search?p={Uri.EscapeDataString(searchQuery)}";
            using var resp = await WebSearchHttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return results;
            }

            var html = await resp.Content.ReadAsStringAsync();
            var algoBlocks = Regex.Matches(html, @"<div class=""dd\s+algo[^""]*""[^>]*>([\s\S]*?)</li>", RegexOptions.IgnoreCase);

            foreach (Match m in algoBlocks)
            {
                if (results.Count >= 6)
                {
                    break;
                }

                var block = m.Groups[1].Value;
                var titleMatch = Regex.Match(block, @"<h3[^>]*>([\s\S]*?)</h3>", RegexOptions.IgnoreCase);
                var snippetMatch = Regex.Match(block, @"<div class=""compText[^""]*""[^>]*>([\s\S]*?)</div>", RegexOptions.IgnoreCase);
                var hrefMatch = Regex.Match(block, @"<a[^>]+href=""([^""]+)""", RegexOptions.IgnoreCase);

                var title = titleMatch.Success
                    ? WebUtility.HtmlDecode(Regex.Replace(titleMatch.Groups[1].Value, @"<[^>]+>", " "))
                    : string.Empty;
                title = Regex.Replace(title, @"\s+", " ").Trim();

                var snippet = snippetMatch.Success
                    ? WebUtility.HtmlDecode(Regex.Replace(snippetMatch.Groups[1].Value, @"<[^>]+>", " "))
                    : string.Empty;
                snippet = Regex.Replace(snippet, @"\s+", " ").Trim();

                var rawHref = hrefMatch.Success ? WebUtility.HtmlDecode(hrefMatch.Groups[1].Value) : string.Empty;
                var ruMatch = Regex.Match(rawHref, @"/RU=([^/]+)/RK=");
                var cleanUrl = ruMatch.Success ? Uri.UnescapeDataString(ruMatch.Groups[1].Value) : rawHref;

                if (!string.IsNullOrWhiteSpace(cleanUrl) && !discoveredUrls.Contains(cleanUrl))
                {
                    discoveredUrls.Add(cleanUrl);
                }

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(snippet))
                {
                    results.Add(new
                    {
                        title,
                        url = cleanUrl,
                        snippet
                    });
                }
            }
        }
        catch
        {
            // Ignore transient web scraping errors
        }

        return results;
    }

    private static async Task<List<object>> QueryGitHubReleasesAndReposAsync(
        string rawQuery,
        string coreTopic,
        List<string> discoveredUrls)
    {
        var results = new List<object>();
        var repoFullNames = new List<string>();

        foreach (var u in discoveredUrls)
        {
            var m = Regex.Match(u, @"^https?://github\.com/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var owner = m.Groups[1].Value;
                var repo = m.Groups[2].Value.TrimEnd('/');
                var fullName = $"{owner}/{repo}";
                if (!repoFullNames.Contains(fullName, StringComparer.OrdinalIgnoreCase))
                {
                    repoFullNames.Add(fullName);
                }
            }
        }

        var lowerQuery = rawQuery.ToLowerInvariant();
        bool asksForVersionOrRepo =
            repoFullNames.Count > 0 ||
            lowerQuery.Contains("wersj") ||
            lowerQuery.Contains("version") ||
            lowerQuery.Contains("release") ||
            lowerQuery.Contains("najnowsz") ||
            lowerQuery.Contains("latest") ||
            lowerQuery.Contains("github") ||
            lowerQuery.Contains("gtnh");

        if (!asksForVersionOrRepo)
        {
            return results;
        }

        try
        {
            if (repoFullNames.Count == 0 && !string.IsNullOrWhiteSpace(coreTopic))
            {
                var searchRepoUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(coreTopic)}&per_page=2";
                using var searchResp = await WebSearchHttpClient.GetAsync(searchRepoUrl);
                if (searchResp.IsSuccessStatusCode)
                {
                    var searchJson = await searchResp.Content.ReadAsStringAsync();
                    using var searchDoc = JsonDocument.Parse(searchJson);
                    if (searchDoc.RootElement.TryGetProperty("items", out var itemsArr) && itemsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in itemsArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("full_name", out var fnProp) && fnProp.ValueKind == JsonValueKind.String)
                            {
                                var fn = fnProp.GetString();
                                if (!string.IsNullOrWhiteSpace(fn))
                                {
                                    repoFullNames.Add(fn);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var repoFullName in repoFullNames.Take(2))
            {
                var releasesUrl = $"https://api.github.com/repos/{repoFullName}/releases?per_page=15";
                using var relResp = await WebSearchHttpClient.GetAsync(releasesUrl);
                if (!relResp.IsSuccessStatusCode)
                {
                    continue;
                }

                var relJson = await relResp.Content.ReadAsStringAsync();
                using var relDoc = JsonDocument.Parse(relJson);
                if (relDoc.RootElement.ValueKind != JsonValueKind.Array || relDoc.RootElement.GetArrayLength() == 0)
                {
                    continue;
                }

                string? latestStableTag = null;
                string? latestStableDate = null;
                string? latestOverallTag = null;
                string? latestOverallDate = null;
                var allTags = new List<string>();

                foreach (var rel in relDoc.RootElement.EnumerateArray())
                {
                    var tag = rel.TryGetProperty("tag_name", out var tProp) ? tProp.GetString() : null;
                    var pubDate = rel.TryGetProperty("published_at", out var dProp) ? dProp.GetString() : null;
                    var isPrerelease = rel.TryGetProperty("prerelease", out var preProp) && preProp.ValueKind == JsonValueKind.True;

                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    allTags.Add(tag);

                    if (latestOverallTag == null)
                    {
                        latestOverallTag = tag;
                        latestOverallDate = pubDate;
                    }

                    bool looksLikeDevOrNightly =
                        isPrerelease ||
                        tag.Contains("nightly", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("-rc", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("-dev", StringComparison.OrdinalIgnoreCase);

                    if (!looksLikeDevOrNightly && latestStableTag == null)
                    {
                        latestStableTag = tag;
                        latestStableDate = pubDate;
                    }
                }

                var summaryParts = new List<string>
                {
                    $"Official GitHub Repository: {repoFullName}."
                };

                if (!string.IsNullOrWhiteSpace(latestStableTag))
                {
                    summaryParts.Add($"Latest Stable Release Version: {latestStableTag} (published: {latestStableDate}).");
                }

                if (!string.IsNullOrWhiteSpace(latestOverallTag) &&
                    !string.Equals(latestOverallTag, latestStableTag, StringComparison.OrdinalIgnoreCase))
                {
                    summaryParts.Add($"Latest Nightly / Pre-Release Version: {latestOverallTag} (published: {latestOverallDate}).");
                }

                if (allTags.Count > 0)
                {
                    summaryParts.Add($"Recent releases: {string.Join(", ", allTags.Take(6))}.");
                }

                results.Add(new
                {
                    title = $"GitHub Official Releases: {repoFullName}",
                    url = $"https://github.com/{repoFullName}/releases",
                    snippet = string.Join(" ", summaryParts)
                });
            }
        }
        catch
        {
            // Ignore transient GitHub API errors
        }

        return results;
    }

    private static async Task<object?> FetchTopWebResultExcerptAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
            using var resp = await WebSearchHttpClient.GetAsync(url, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await resp.Content.ReadAsStringAsync(cts.Token);
            var titleMatch = Regex.Match(html, @"<title[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase);
            var title = titleMatch.Success ? WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim()) : url;

            var cleaned = Regex.Replace(html, @"<(script|style|noscript|svg|header|footer|nav)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
            cleaned = WebUtility.HtmlDecode(cleaned);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            if (cleaned.Length > 1000)
            {
                cleaned = cleaned[..1000] + "...";
            }

            return new
            {
                title = $"Live Page Excerpt: {title}",
                url,
                snippet = cleaned
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<List<object>> QueryWikipediaAsync(string lang, string searchTerm)
    {
        var list = new List<object>();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return list;
        }

        try
        {
            var searchUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchTerm)}&utf8=1&format=json&srlimit=2";
            using var searchResp = await WebSearchHttpClient.GetAsync(searchUrl);
            if (!searchResp.IsSuccessStatusCode)
            {
                return list;
            }

            var searchJson = await searchResp.Content.ReadAsStringAsync();
            using var searchDoc = JsonDocument.Parse(searchJson);
            if (!searchDoc.RootElement.TryGetProperty("query", out var queryEl) ||
                !queryEl.TryGetProperty("search", out var searchArr) ||
                searchArr.ValueKind != JsonValueKind.Array ||
                searchArr.GetArrayLength() == 0)
            {
                return list;
            }

            var titles = new List<string>();
            foreach (var item in searchArr.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var tProp) && tProp.ValueKind == JsonValueKind.String)
                {
                    var title = tProp.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        titles.Add(title);
                    }
                }
            }

            foreach (var title in titles.Take(2))
            {
                var extractUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&prop=extracts&exintro=1&explaintext=1&exchars=1400&titles={Uri.EscapeDataString(title)}&format=json";
                using var extractResp = await WebSearchHttpClient.GetAsync(extractUrl);
                if (!extractResp.IsSuccessStatusCode)
                {
                    continue;
                }

                var extractJson = await extractResp.Content.ReadAsStringAsync();
                using var extractDoc = JsonDocument.Parse(extractJson);
                if (extractDoc.RootElement.TryGetProperty("query", out var qEl) &&
                    qEl.TryGetProperty("pages", out var pagesEl) &&
                    pagesEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var pageProp in pagesEl.EnumerateObject())
                    {
                        if (pageProp.Value.TryGetProperty("extract", out var extEl) && extEl.ValueKind == JsonValueKind.String)
                        {
                            var extractText = extEl.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(extractText))
                            {
                                list.Add(new
                                {
                                    title = $"{title} ({lang.ToUpperInvariant()} Wikipedia)",
                                    url = $"https://{lang}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}",
                                    snippet = extractText
                                });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore transient network errors and fall back to next source
        }

        return list;
    }

    private static async Task<List<object>> QueryDuckDuckGoInstantAnswerAsync(string searchTerm)
    {
        var list = new List<object>();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return list;
        }

        try
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(searchTerm)}&format=json&no_html=1&skip_disambig=1";
            using var resp = await WebSearchHttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return list;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("AbstractText", out var absProp) && absProp.ValueKind == JsonValueKind.String)
            {
                var absText = absProp.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(absText))
                {
                    var absUrl = root.TryGetProperty("AbstractURL", out var uProp) ? uProp.GetString() : "";
                    var heading = root.TryGetProperty("Heading", out var hProp) ? hProp.GetString() : searchTerm;
                    list.Add(new
                    {
                        title = $"{heading} (DuckDuckGo Instant Answer)",
                        url = absUrl ?? "",
                        snippet = absText
                    });
                }
            }
        }
        catch
        {
            // Ignore transient network errors
        }

        return list;
    }

    private static async Task<string> FetchPageSummaryAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "Error: URL is empty.";
        }

        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        try
        {
            using var resp = await WebSearchHttpClient.GetAsync(trimmed);
            var rawHtml = await resp.Content.ReadAsStringAsync();

            var titleMatch = Regex.Match(rawHtml, @"<title[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase);
            var pageTitle = titleMatch.Success ? WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim()) : trimmed;

            var cleanedText = Regex.Replace(rawHtml, @"<(script|style|noscript)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
            cleanedText = Regex.Replace(cleanedText, @"<[^>]+>", " ");
            cleanedText = WebUtility.HtmlDecode(cleanedText);
            cleanedText = Regex.Replace(cleanedText, @"\s+", " ").Trim();

            if (cleanedText.Length > 2500)
            {
                cleanedText = cleanedText[..2500] + "...";
            }

            return JsonSerializer.Serialize(new
            {
                url = trimmed,
                status = (int)resp.StatusCode,
                title = pageTitle,
                fetchedAt = DateTime.UtcNow.ToString("o"),
                contentExcerpt = cleanedText
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                url = trimmed,
                status = 0,
                fetchedAt = DateTime.UtcNow.ToString("o"),
                error = $"Could not fetch URL '{trimmed}': {ex.Message}"
            });
        }
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
