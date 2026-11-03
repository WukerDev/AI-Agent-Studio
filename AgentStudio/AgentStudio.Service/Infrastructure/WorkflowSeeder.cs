using AgentStudio.Service.Model;
using AgentStudio.Service.Protos;
using Microsoft.EntityFrameworkCore;

namespace AgentStudio.Service.Infrastructure;

public static class WorkflowSeeder
{
    public static readonly string[] SampleFlowIds =
    [
        "default-flow",
        "multi-agent-research",
        "gemini-assistant-flow",
        "code-review-flow",
        "mcp-calculator-time-flow"
    ];

    public static async Task SeedSampleWorkflowsAsync(
        ApplicationDbContext db,
        string ownerId = "guest-user",
        CancellationToken cancellationToken = default)
    {
        var activeOwnerId = await db.Workflows
            .Select(w => w.OwnerId)
            .FirstOrDefaultAsync(o => !string.IsNullOrEmpty(o) && o != "guest-user", cancellationToken)
            ?? ownerId;

        await SeedFlowsForOwnerAsync(db, activeOwnerId, cancellationToken);
    }

    private static async Task SeedFlowsForOwnerAsync(
        ApplicationDbContext db,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        await SeedFlowIfMissingAsync(
            db,
            ownerId,
            flowId: "default-flow",
            flowName: "1. Asystent AI + MCP + System Prompt (Gemma3 12B)",
            description: "Kompletny przepływ w stylu n8n: Węzeł AI Agent z podłączonymi portami [model], [prompt] oraz [tool].",
            nodes:
            [
                new AgentNodeEntity
                {
                    Id = "seed-def-label",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "Asystent AI w architekturze n8n (Porty: Model, Prompt, Tool)",
                    Type = (int)NodeType.Label,
                    PositionX = 100,
                    PositionY = 20,
                    Endpoint = "medium",
                    ModelProvider = "#8B5CF6",
                    SystemPrompt = "Węzły Model, System Prompt i MCP łączą się z dedykowanymi portami na dole węzła AI Agent."
                },
                new AgentNodeEntity
                {
                    Id = "seed-def-agent",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "Główny Agent AI",
                    Type = (int)NodeType.Agent,
                    PositionX = 290,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-def-model",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "Ollama Gemma 3 (12B)",
                    Type = (int)NodeType.Model,
                    PositionX = 80,
                    PositionY = 330,
                    ModelProvider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.3,
                    Endpoint = "http://localhost:11434"
                },
                new AgentNodeEntity
                {
                    Id = "seed-def-prompt",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "System Prompt (Polski Asystent)",
                    Type = (int)NodeType.Prompt,
                    PositionX = 300,
                    PositionY = 330,
                    SystemPrompt = "Jesteś inteligentnym asystentem AI w środowisku AI Agent Studio. Odpowiadasz precyzyjnie po polsku.\nZadanie użytkownika: {{input}}\nKorzystaj z podłączonych narzędzi MCP (calculate_expression, get_current_time, get_system_info, search_web), aby podawać dokładne dane."
                },
                new AgentNodeEntity
                {
                    Id = "seed-def-mcp",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "Wbudowane Narzędzia MCP",
                    Type = (int)NodeType.McpConnector,
                    PositionX = 520,
                    PositionY = 330,
                    McpServerUrl = "builtin://tools",
                    McpAuthToken = "",
                    McpEnabledTools = ["calculate_expression", "get_current_time", "get_system_info", "search_web"]
                },
                new AgentNodeEntity
                {
                    Id = "seed-def-note",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    Name = "Przykładowe prompty (Run Flow)",
                    Type = (int)NodeType.Note,
                    PositionX = 690,
                    PositionY = 120,
                    Endpoint = "#F59E0B",
                    SystemPrompt = "Przykładowe prompty do przetestowania:\n1. \"Użyj narzędzia calculate_expression aby obliczyć: (145 * 28) + 350\"\n2. \"Uruchom narzędzie get_system_info i podaj system operacyjny, wersję .NET oraz liczbę rdzeni CPU\"\n3. \"Sprawdź aktualny czas UTC za pomocą get_current_time\""
                }
            ],
            edges:
            [
                new WorkflowEdgeEntity
                {
                    Id = "edge-def-model-agent",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-def-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-def-agent",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-def-prompt-agent",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-def-prompt",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-def-agent",
                    TargetHandle = "prompt"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-def-mcp-agent",
                    FlowId = "default-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-def-mcp",
                    SourceHandle = "tool-out",
                    TargetNodeId = "seed-def-agent",
                    TargetHandle = "tool"
                }
            ],
            cancellationToken);

        await SeedFlowIfMissingAsync(
            db,
            ownerId,
            flowId: "multi-agent-research",
            flowName: "2. Wieloagentowy Pipeline: Badacz -> Redaktor",
            description: "Sekwencyjny przepływ 2 agentów połączonych portami pipeline (agent-out -> agent-in) z podpiętymi węzłami Model, Prompt i Tool.",
            nodes:
            [
                new AgentNodeEntity
                {
                    Id = "seed-res-label",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Pipeline Wieloagentowy: Agent 1 (Badacz) -> Agent 2 (Redaktor)",
                    Type = (int)NodeType.Label,
                    PositionX = 80,
                    PositionY = 20,
                    Endpoint = "medium",
                    ModelProvider = "#3B82F6",
                    SystemPrompt = "Agent 1 przekazuje wynik prawym portem wyjściowym do lewego portu wejściowego Agenta 2."
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-agent1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Agent 1: Badacz (Researcher)",
                    Type = (int)NodeType.Agent,
                    PositionX = 140,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-agent2",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Agent 2: Redaktor (Summarizer)",
                    Type = (int)NodeType.Agent,
                    PositionX = 520,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-model",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Ollama Gemma 3 (12B)",
                    Type = (int)NodeType.Model,
                    PositionX = 60,
                    PositionY = 340,
                    ModelProvider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.4,
                    Endpoint = "http://localhost:11434"
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-prompt1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Prompt: Badacz Techniczny",
                    Type = (int)NodeType.Prompt,
                    PositionX = 260,
                    PositionY = 340,
                    SystemPrompt = "Jesteś analitykiem technicznym (Researcher). Zbierz 3 najważniejsze fakty techniczne na temat: {{input}}"
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-mcp1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Web & Knowledge MCP",
                    Type = (int)NodeType.McpConnector,
                    PositionX = 450,
                    PositionY = 340,
                    McpServerUrl = "builtin://tools",
                    McpEnabledTools = ["search_web", "execute_query"]
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-prompt2",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Prompt: Redaktor Raportu",
                    Type = (int)NodeType.Prompt,
                    PositionX = 650,
                    PositionY = 340,
                    SystemPrompt = "Jesteś redaktorem naczelnym (Summarizer). Na podstawie danych od pierwszego agenta przygotuj zwięzłe podsumowanie w punktach po polsku."
                },
                new AgentNodeEntity
                {
                    Id = "seed-res-note",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    Name = "Przykładowe prompty (Badacz -> Redaktor)",
                    Type = (int)NodeType.Note,
                    PositionX = 830,
                    PositionY = 120,
                    Endpoint = "#0EA5E9",
                    SystemPrompt = "Przykładowe prompty:\n1. \"Zbadaj kluczowe różnice między komunikacją gRPC a REST API w mikroserwisach.\"\n2. \"Przeanalizuj zastosowania indeksów B-Tree oraz JSONB GIN w bazie PostgreSQL.\"\n3. \"Opisz wzorzec orkiestracji wieloagentowej ReAct oraz protokół MCP.\""
                }
            ],
            edges:
            [
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-a1-a2",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-agent1",
                    SourceHandle = "agent-out",
                    TargetNodeId = "seed-res-agent2",
                    TargetHandle = "agent-in"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-m-a1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-res-agent1",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-p1-a1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-prompt1",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-res-agent1",
                    TargetHandle = "prompt"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-mcp-a1",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-mcp1",
                    SourceHandle = "tool-out",
                    TargetNodeId = "seed-res-agent1",
                    TargetHandle = "tool"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-m-a2",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-res-agent2",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-res-p2-a2",
                    FlowId = "multi-agent-research",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-res-prompt2",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-res-agent2",
                    TargetHandle = "prompt"
                }
            ],
            cancellationToken);

        await SeedFlowIfMissingAsync(
            db,
            ownerId,
            flowId: "gemini-assistant-flow",
            flowName: "3. Google Gemini 3.5 Flash-Lite + MCP Tools",
            description: "Przepływ z najnowszym, szybkim modelem Google Gemini API (gemini-3.5-flash-lite) oraz narzędziami MCP.",
            nodes:
            [
                new AgentNodeEntity
                {
                    Id = "seed-gem-label",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "Google Gemini API (gemini-3.5-flash-lite — Najnowszy i Najszybszy Model) + MCP",
                    Type = (int)NodeType.Label,
                    PositionX = 100,
                    PositionY = 20,
                    Endpoint = "medium",
                    ModelProvider = "#10B981",
                    SystemPrompt = "Używa najnowszego, aktywnego modelu gemini-3.5-flash-lite z podłączonym kluczem API."
                },
                new AgentNodeEntity
                {
                    Id = "seed-gem-agent",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "Agent Google Gemini",
                    Type = (int)NodeType.Agent,
                    PositionX = 290,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-gem-model",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "Gemini 3.5 Flash-Lite",
                    Type = (int)NodeType.Model,
                    PositionX = 80,
                    PositionY = 330,
                    ModelProvider = "Gemini",
                    ModelName = "gemini-3.5-flash-lite",
                    Temperature = 0.5,
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/"
                },
                new AgentNodeEntity
                {
                    Id = "seed-gem-prompt",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "System Prompt (Gemini Expert)",
                    Type = (int)NodeType.Prompt,
                    PositionX = 300,
                    PositionY = 330,
                    SystemPrompt = "Jesteś szybkim asystentem AI działającym na modelu Google Gemini 3.5 Flash-Lite.\nAktualna data: {{date}}\nZapytanie użytkownika: {{input}}\nOdpowiadaj rzeczowo po polsku i korzystaj z narzędzi MCP."
                },
                new AgentNodeEntity
                {
                    Id = "seed-gem-mcp",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "Narzędzia MCP dla Gemini",
                    Type = (int)NodeType.McpConnector,
                    PositionX = 520,
                    PositionY = 330,
                    McpServerUrl = "builtin://tools",
                    McpEnabledTools = ["calculate_expression", "get_current_time", "get_system_info", "search_web", "list_dir"]
                },
                new AgentNodeEntity
                {
                    Id = "seed-gem-note",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    Name = "Klucz API & Przykładowe Prompty",
                    Type = (int)NodeType.Note,
                    PositionX = 690,
                    PositionY = 120,
                    Endpoint = "#10B981",
                    SystemPrompt = "Wklej swój klucz API Gemini w prawym panelu Inspector dla węzła 'Gemini 3.5 Flash-Lite' (lub ustaw zmienną środowiskową GEMINI_API_KEY).\nPrzykładowe prompty:\n1. \"Oblicz za pomocą calculate_expression wyrażenie (512 * 32) + 1024\"\n2. \"Pobierz informacje o systemie przez get_system_info\"\n3. \"Opisz zalety modelu Gemini 3.5 Flash-Lite\""
                }
            ],
            edges:
            [
                new WorkflowEdgeEntity
                {
                    Id = "edge-gem-model-agent",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-gem-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-gem-agent",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-gem-prompt-agent",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-gem-prompt",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-gem-agent",
                    TargetHandle = "prompt"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-gem-mcp-agent",
                    FlowId = "gemini-assistant-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-gem-mcp",
                    SourceHandle = "tool-out",
                    TargetNodeId = "seed-gem-agent",
                    TargetHandle = "tool"
                }
            ],
            cancellationToken);

        await SeedFlowIfMissingAsync(
            db,
            ownerId,
            flowId: "code-review-flow",
            flowName: "4. Architekt & Code Reviewer (File System MCP)",
            description: "Przepływ do analizy kodu i diagnostyki środowiska z wykorzystaniem narzędzi MCP (list_dir, read_file, get_system_info).",
            nodes:
            [
                new AgentNodeEntity
                {
                    Id = "seed-cr-label",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "Architekt Oprogramowania & Code Reviewer (File System MCP)",
                    Type = (int)NodeType.Label,
                    PositionX = 100,
                    PositionY = 20,
                    Endpoint = "medium",
                    ModelProvider = "#F97316",
                    SystemPrompt = "Port [tool] udostępnia agentowi narzędzia: list_dir, read_file, get_system_info, execute_query."
                },
                new AgentNodeEntity
                {
                    Id = "seed-cr-agent",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "Agent Code Reviewer",
                    Type = (int)NodeType.Agent,
                    PositionX = 290,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-cr-model",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "Ollama Gemma 3 (12B)",
                    Type = (int)NodeType.Model,
                    PositionX = 80,
                    PositionY = 330,
                    ModelProvider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.2,
                    Endpoint = "http://localhost:11434"
                },
                new AgentNodeEntity
                {
                    Id = "seed-cr-prompt",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "Persona: Senior .NET & Vue Architect",
                    Type = (int)NodeType.Prompt,
                    PositionX = 300,
                    PositionY = 330,
                    SystemPrompt = "Jesteś głównym architektem oprogramowania (.NET, C#, Vue 3, gRPC-Web, SignalR, PostgreSQL).\nPrzeanalizuj zagadnienie: {{input}}\nGdy użytkownik pyta o pliki projektu lub parametry hosta, wywołaj narzędzia MCP: list_dir, read_file lub get_system_info."
                },
                new AgentNodeEntity
                {
                    Id = "seed-cr-mcp",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "File System & Diagnostics MCP",
                    Type = (int)NodeType.McpConnector,
                    PositionX = 520,
                    PositionY = 330,
                    McpServerUrl = "builtin://tools",
                    McpEnabledTools = ["list_dir", "read_file", "get_system_info", "execute_query"]
                },
                new AgentNodeEntity
                {
                    Id = "seed-cr-note",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    Name = "Przykładowe prompty (Code Review & Pliki)",
                    Type = (int)NodeType.Note,
                    PositionX = 690,
                    PositionY = 120,
                    Endpoint = "#8B5CF6",
                    SystemPrompt = "Przykładowe prompty:\n1. \"Wypisz zawartość katalogu roboczego za pomocą list_dir i opisz strukturę projektu.\"\n2. \"Wywołaj get_system_info i podaj wersję środowiska .NET oraz liczbę rdzeni CPU.\"\n3. \"Wymień 3 zasady zabezpieczania usług gRPC-Web oraz SignalR.\""
                }
            ],
            edges:
            [
                new WorkflowEdgeEntity
                {
                    Id = "edge-cr-model-agent",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-cr-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-cr-agent",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-cr-prompt-agent",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-cr-prompt",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-cr-agent",
                    TargetHandle = "prompt"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-cr-mcp-agent",
                    FlowId = "code-review-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-cr-mcp",
                    SourceHandle = "tool-out",
                    TargetNodeId = "seed-cr-agent",
                    TargetHandle = "tool"
                }
            ],
            cancellationToken);

        await SeedFlowIfMissingAsync(
            db,
            ownerId,
            flowId: "mcp-calculator-time-flow",
            flowName: "5. Tester Narzędzi MCP (Kalkulator, Czas, Diagnostyka)",
            description: "Dedykowany przepływ do testowania wywołań narzędzi MCP (get_system_info, calculate_expression, get_current_time) z lokalnym modelem Gemma3 12B.",
            nodes:
            [
                new AgentNodeEntity
                {
                    Id = "seed-mcp-label",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "Tester Narzędzi MCP (ReAct Tool Calling z Gemma3 12B)",
                    Type = (int)NodeType.Label,
                    PositionX = 100,
                    PositionY = 20,
                    Endpoint = "medium",
                    ModelProvider = "#EC4899",
                    SystemPrompt = "Wywołuje narzędzia MCP (get_system_info, calculate_expression, get_current_time) i prezentuje dane."
                },
                new AgentNodeEntity
                {
                    Id = "seed-mcp-agent",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "Agent Wykonawczy MCP",
                    Type = (int)NodeType.Agent,
                    PositionX = 290,
                    PositionY = 130
                },
                new AgentNodeEntity
                {
                    Id = "seed-mcp-model",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "Ollama Gemma 3 (12B)",
                    Type = (int)NodeType.Model,
                    PositionX = 80,
                    PositionY = 330,
                    ModelProvider = "Ollama",
                    ModelName = "gemma3:12b",
                    Temperature = 0.1,
                    Endpoint = "http://localhost:11434"
                },
                new AgentNodeEntity
                {
                    Id = "seed-mcp-prompt",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "System Prompt (Wymuś Narzędzie MCP)",
                    Type = (int)NodeType.Prompt,
                    PositionX = 300,
                    PositionY = 330,
                    SystemPrompt = "Zawsze używaj odpowiedniego narzędzia MCP (get_system_info dla informacji o systemie, calculate_expression dla obliczeń, get_current_time dla czasu/daty) przed odpowiedzią na: {{input}}"
                },
                new AgentNodeEntity
                {
                    Id = "seed-mcp-connector",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "Pakiet Narzędzi MCP",
                    Type = (int)NodeType.McpConnector,
                    PositionX = 520,
                    PositionY = 330,
                    McpServerUrl = "builtin://tools",
                    McpEnabledTools = ["get_system_info", "calculate_expression", "get_current_time", "search_web", "execute_query"]
                },
                new AgentNodeEntity
                {
                    Id = "seed-mcp-note",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    Name = "Przykładowe prompty (Tester MCP)",
                    Type = (int)NodeType.Note,
                    PositionX = 690,
                    PositionY = 120,
                    Endpoint = "#F43F5E",
                    SystemPrompt = "Przykładowe prompty:\n1. \"Wywołaj narzędzie get_system_info i podaj nazwę systemu operacyjnego, wersję .NET oraz liczbę rdzeni CPU.\"\n2. \"Użyj narzędzia calculate_expression aby policzyć (256 * 64) + 1024.\"\n3. \"Wywołaj narzędzie get_current_time i podaj datę oraz godzinę UTC.\""
                }
            ],
            edges:
            [
                new WorkflowEdgeEntity
                {
                    Id = "edge-mcp-model-agent",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-mcp-model",
                    SourceHandle = "model-out",
                    TargetNodeId = "seed-mcp-agent",
                    TargetHandle = "model"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-mcp-prompt-agent",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-mcp-prompt",
                    SourceHandle = "prompt-out",
                    TargetNodeId = "seed-mcp-agent",
                    TargetHandle = "prompt"
                },
                new WorkflowEdgeEntity
                {
                    Id = "edge-mcp-conn-agent",
                    FlowId = "mcp-calculator-time-flow",
                    OwnerId = ownerId,
                    SourceNodeId = "seed-mcp-connector",
                    SourceHandle = "tool-out",
                    TargetNodeId = "seed-mcp-agent",
                    TargetHandle = "tool"
                }
            ],
            cancellationToken);
    }

    private static async Task SeedFlowIfMissingAsync(
        ApplicationDbContext db,
        string ownerId,
        string flowId,
        string flowName,
        string description,
        List<AgentNodeEntity> nodes,
        List<WorkflowEdgeEntity> edges,
        CancellationToken cancellationToken)
    {
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == flowId, cancellationToken);
        if (workflow == null)
        {
            db.Workflows.Add(new WorkflowEntity
            {
                Id = flowId,
                OwnerId = ownerId,
                Name = flowName,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (workflow.Name == flowId || workflow.Name == "DefaultWorkflow" || workflow.Name.StartsWith("3. Google Gemini"))
        {
            workflow.Name = flowName;
            workflow.Description = description;
        }

        var existingNodeCount = await db.AgentNodes.CountAsync(n => n.FlowId == flowId, cancellationToken);
        if (existingNodeCount == 0)
        {
            foreach (var node in nodes)
            {
                if (!await db.AgentNodes.AnyAsync(n => n.Id == node.Id, cancellationToken))
                {
                    db.AgentNodes.Add(node);
                }
            }

            foreach (var edge in edges)
            {
                if (!await db.WorkflowEdges.AnyAsync(e => e.Id == edge.Id, cancellationToken))
                {
                    db.WorkflowEdges.Add(edge);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
