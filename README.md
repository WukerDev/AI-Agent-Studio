# AI Agent Studio – Wizualny Kreator i Orkiestrator Agentów AI

![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?style=flat-square&logo=dotnet)
![Vue.js](https://img.shields.io/badge/Vue.js-3.x-4FC08D?style=flat-square&logo=vuedotjs)
![Vuetify](https://img.shields.io/badge/Vuetify-3.x-1867C0?style=flat-square&logo=vuetify)
![YARP](https://img.shields.io/badge/YARP-Reverse_Proxy-0078D4?style=flat-square&logo=microsoft)
![gRPC](https://img.shields.io/badge/gRPC-gRPC--Web-244C5A?style=flat-square&logo=google)
![Protobuf](https://img.shields.io/badge/Protobuf-Protocol_Buffers-4285F4?style=flat-square&logo=google)
![MS Agents AI](https://img.shields.io/badge/Microsoft-Agents_AI-00A4EF?style=flat-square&logo=microsoft)
![MCP](https://img.shields.io/badge/MCP-Model_Context_Protocol-8A2BE2?style=flat-square)
![Docker](https://img.shields.io/badge/Docker-Container-2496ED?style=flat-square&logo=docker)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-336791?style=flat-square&logo=postgresql)
![Keycloak](https://img.shields.io/badge/Keycloak-IAM-4D4D4D?style=flat-square&logo=keycloak)
![Grafana](https://img.shields.io/badge/Grafana-Observability-F46800?style=flat-square&logo=grafana)

---

## 📌 O Projekcie

**AI Agent Studio** to nowoczesna platforma oparta na architekturze mikroserwisowej, umożliwiająca wizualne projektowanie, konfigurowanie i uruchamianie autonomicznych oraz wieloagentowych systemów sztucznej inteligencji.

Głównym założeniem systemu jest pełna separacja warstwy prezentacji od logiki wykonawczej:
* Użytkownik za pomocą interfejsu graficznego typu *node-based* (**VueFlow**) buduje przepływy pracy agentów metodą drag-and-drop, definiuje ich zachowanie, dobiera modele LLM oraz podpina zewnętrzne narzędzia z internetu za pomocą protokołu **Model Context Protocol (MCP)**.
* Cała komunikacja przeglądarki z systemem przepływa przez bramkę **Frontend Gateway (YARP Reverse Proxy)** na wspólnym porcie `:5000`.
* Egzekucja agentów odbywa się w wysoko wydajnym środowisku backendowym **.NET 11** z wykorzystaniem ekosystemu **Microsoft Agents AI** (`Microsoft.Agents.AI`) oraz klienta MCP SDK.

```mermaid
flowchart TD
    User["Przeglądarka / SPA Client"] -->|HTTP / WS / gRPC-Web :5000| Gateway["AgentStudio.Gateway (YARP)"]
    Gateway -->|gRPC-Web & SignalR| Service["AgentStudio.Service (.NET 11)"]
    Gateway -->|OIDC / Auth| Keycloak["Keycloak IAM (:8080)"]
    Gateway -->|SPA Assets Fallback| ClientService["AgentStudio.Client (:8080)"]
    Gateway -->|Dashboardy| Grafana["Grafana (:3000)"]
    Service -->|MS Agents AI| LLMs["LLM Providers (OpenAI, Ollama, Custom API)"]
    Service -->|Model Context Protocol| McpServers["Zewnętrzne Serwery Narzędzi MCP"]
    Service -->|EF Core Npgsql| Postgres[("Baza PostgreSQL (:5432)")]
```

---

## 🚀 Główne Funkcjonalności

1. **Wizualny Kreator Przepływów (VueFlow):**
   * Projektowanie architektury agentów oraz ich wzajemnych relacji metodą drag-and-drop.
   * Obsługa 4 dedykowanych węzłów: **Węzeł Agenta**, **Węzeł Modelu LLM**, **Węzeł Promptu Systemowego** oraz **Węzeł Konektora MCP**.
2. **Elastyczne Podłączanie Modeli AI:**
   * Wsparcie dla chmurowych dostawców (**OpenAI**: `gpt-4o`, `gpt-4o-mini`, itp.).
   * Pełne wsparcie dla modeli lokalnych (**Ollama**: `llama3.2`, `mistral`, itp.) bez konieczności płacenia za API.
   * Wsparcie dla niestandardowych endpointów zgodnych z OpenAI API (vLLM, LM Studio, Azure OpenAI).
3. **Integracja z MCP (Model Context Protocol):**
   * Dynamiczne podłączanie serwerów narzędziowych MCP (HTTP / SSE / SSE Transport).
   * Automatyczna dyskryminacja narzędzi, mapowanie ich na `AITool` w Microsoft Agents AI i udostępnianie agentom w pętli myślenia.
4. **Wysokowydajny gRPC-Web & Real-Time SignalR:**
   * Silnie typowane kontrakty Protocol Buffers (`agent_node.proto`).
   * Zapis i odczyt konfiguracji grafu przez gRPC-Web.
   * Asynchroniczne strumieniowanie logów egzekucji agenta krok po kroku oraz odpowiedzi AI na żywo do panelu konsoli frontendu.
5. **Centralna Bramka YARP (Frontend Gateway):**
   * Jeden port wejściowy `:5000` obsługujący gRPC-Web, SignalR (WebSockets), Keycloak i pliki SPA.
   * Całkowita eliminacja problemów z CORS i zróżnicowanymi portami.
6. **Pełne Bezpieczeństwo i Tożsamość (IAM):**
   * Centralne zarządzanie tożsamością z użyciem Keycloak (realm `agent-studio`, klient OIDC `portal`).

---

## 🛠️ Stos Technologiczny

### Frontend
* **Framework:** Vue 3 (Composition API, `<script setup lang="ts">`, TypeScript)
* **Zarządzanie Stanem:** Pinia (`store.ts`, `notifications.ts`)
* **Architektura:** Top-to-bottom ViewModel (`GlobalViewModel.ts`, `AuthViewModel.ts`)
* **Komponenty UI:** Vuetify 3 (motywy `agentStudioDarkTheme` i `agentStudioLightTheme`)
* **Silnik Grafowy:** VueFlow 1.x (`@vue-flow/core`, `@vue-flow/background`, `@vue-flow/controls`, `@vue-flow/minimap`)
* **Komunikacja:** Protocol Buffers & gRPC-Web (`@protobuf-ts/grpcweb-transport`), SignalR (`@microsoft/signalr`)
* **Powiadomienia:** Centralny store powiadomień i komponent Toast Snackbar
* **Internacjonalizacja & Czas:** `vue-i18n` (słowniki `pl.json`, `en.json`), `luxon`
* **Testy Frontendu:** Vitest + JSDOM (`@vue/test-utils`)

### Gateway & Backend
* **Frontend Gateway:** .NET 11 Web App z **YARP Reverse Proxy** (`Yarp.ReverseProxy 2.3.0`)
* **Rdzeń Serwisu:** .NET 11 (C#), ASP.NET Core
* **Orkiestracja AI:** **Microsoft Agents AI** (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.Workflows`, `Microsoft.Agents.AI.OpenAI`)
* **Integracja MCP:** Model Context Protocol C# SDK (`ModelContextProtocol`, `ModelContextProtocol.Core`)
* **Komunikacja:** gRPC, gRPC-Web Middleware, Protobuf (`Grpc.AspNetCore.Web`)
* **Baza Danych:** PostgreSQL 15/16 z Entity Framework Core (`Npgsql.EntityFrameworkCore.PostgreSQL`)
* **Testy Integracyjne:** xUnit, FluentAssertions, `WebApplicationFactory`, **Testcontainers (PostgreSQL)**

### Infrastruktura & CI/CD
* **Konteneryzacja:** Docker & Docker Compose (`compose.yaml`)
* **Tożsamość i Dostęp (IAM):** Keycloak 26+ (automatyczny import realmu `Deploy/keycloak/agent-studio-realm.json`)
* **Monitoring:** Grafana
* **CI/CD:** GitHub Actions (`.github/workflows/ci.yml`) z kompilacją Release, testami integracyjnymi .NET i testami TypeScript

---

## 🧠 Jak działa Kreator Agentów i jak podłączyć Model AI?

### 1. Elementy Składowe Przepływu (Węzły na Kanwie)
Kanwa kreatora pozwala łączyć 4 rodzaje węzłów:
* 🤖 **AI Agent (Czerwony):** Główny węzeł decyzyjny reprezentujący autonomicznego agenta `ChatClientAgent`.
* 🧠 **LLM Model (Niebieski):** Węzeł definiujący model językowy (OpenAI, Ollama, Custom API), temperaturę oraz adres API.
* 📝 **System Prompt (Pomarańczowy):** Węzeł wstrzykujący instrukcje bazowe, rolę i zasady postępowania agenta.
* 🔌 **MCP Connector (Zielony):** Węzeł łączący agenta z zewnętrznym serwerem narzędziowym Model Context Protocol (np. wyszukiwarka internetowa, baza danych, system plików).

---

### 2. Sposób 1: Wizualne Podłączenie na Kanwie (Rekomendowane)
1. Kliknij **Add Node -> LLM Model** na pasku narzędzi lub wybierz kartę *LLM Model* z lewego panelu.
2. Kliknij **Add Node -> AI Agent** i umieść go obok modelu.
3. Przeciągnij krawędź z prawego punktu węzła **LLM Model** (uchwyt wyjściowy) do lewego punktu węzła **AI Agent** (uchwyt wejściowy).
4. Kliknij ikonę zębatki ⚙️ na węźle modelu, aby otworzyć okno konfiguracji:
   * Wybierz **Provider**: `OpenAI`, `Ollama` lub `Custom`.
   * Wpisz **Model Name**: np. `gpt-4o` lub `llama3.2`.
   * Ustaw **Temperature**: np. `0.7`.
5. Kliknij **Save Changes**. Konfiguracja zostanie natychmiast utrwalona w bazie danych przez gRPC.

---

### 3. Sposób 2: Bezpośrednia Konfiguracja w Węźle Agenta
Każdy węzeł **AI Agent** posiada wbudowaną konfigurację awaryjną:
1. Kliknij ikonę zębatki ⚙️ bezpośrednio na węźle Agenta.
2. W oknie konfiguracji możesz od razu wybrać dostawcę modelu, wpisać prompt systemowy oraz podać URL serwera MCP.
3. Jeśli węzeł Agenta nie ma podłączonego osobnego węzła modelu, silnik automatycznie skorzysta z konfiguracji bezpośredniej lub domyślnej z pliku konfiguracyjnego `appsettings.json`.

---

### 4. Konfiguracja Dostawców Modeli (Przykłady)

#### A. OpenAI (Chmura)
* **Provider:** `OpenAI`
* **Model Name:** `gpt-4o`, `gpt-4o-mini`, `o1-mini`
* **Klucz API:** Ustaw zmienną środowiskową `OPENAI_API_KEY=sk-...` lub w `appsettings.json` pod `AiProviders:OpenAI:ApiKey`.
* **Endpoint:** (Domyślnie `https://api.openai.com/v1`)

#### B. Ollama (Lokalnie na Twoim komputerze)
* Uruchom Ollama na komputerze (`ollama run llama3.2`).
* **Provider:** `Ollama`
* **Model Name:** `llama3.2`, `mistral`, `deepseek-r1`
* **Custom API Endpoint:** `http://localhost:11434` (lub `http://host.docker.internal:11434` w kontenerze).

#### C. Custom API / vLLM / LM Studio / Azure OpenAI
* **Provider:** `Custom`
* **Model Name:** np. `local-model`
* **Custom API Endpoint:** np. `http://localhost:1234/v1`

---

### 5. Uruchomienie Przepływu Agenta
1. Kliknij przycisk **Run Flow...** na pasku narzędzi.
2. W oknie dialogowym wpisz opcjonalne zadanie lub zapytanie wejściowe (np. *„Wyszukaj najnowsze artykuły o AI i przygotuj podsumowanie”*).
3. Kliknij **Execute**.
4. W dolnym panelu konsoli (**Console**) zobaczysz strumieniowane na żywo logi z silnika:
   * Rozpoznanie konfiguracji modelu i promptu,
   * Nawiązanie połączenia z serwerami MCP i podpięcie narzędzi,
   * Wykonanie pętli myślenia agenta w Microsoft Agents AI.
5. W zakładce **Output** pojawi się ostateczna odpowiedź wygenerowana przez Agenta.

---

## 👥 Zespół Projektowy i Podział Zadań

| Członek Zespołu | Role w Projekcie | Główne Obszary Odpowiedzialności |
| :--- | :--- | :--- |
| **Wiktor** | **Project Leader, Head Designer** | • Nadzór nad architekturą systemu i kierunkiem rozwoju projektu.<br>• Projektowanie UI/UX, architektury komponentów oraz przepływów w VueFlow.<br>• Definiowanie standardów kodowania (architektura *top-to-bottom*, separacja ViewModeli i Store'ów).<br>• Projektowanie kontraktów **Protobuf** oraz integracji **gRPC-Web** na styku Frontend – Backend. |
| **Miłosz** | **Head Backend Developer, Microservice Developer** | • Projektowanie i implementacja mikroserwisów w oparciu o **.NET 11**.<br>• Tworzenie usług **gRPC**, obsługa warstwy **gRPC-Web** oraz definicji kontraktów **Protocol Buffers**.<br>• Projektowanie schematów bazy danych **PostgreSQL** oraz warstwy persystencji.<br>• Integracja backendu z serwerem autoryzacji **Keycloak**. |
| **Sebastian** | **Archiwista, Tester, AI Agents Developer, Co-Frontend Developer** | • Implementacja logiki agentów przy użyciu **Microsoft Agents AI** oraz klienta **MCP**.<br>• Wsparcie rozwoju warstwy frontendowej (widoki, komponenty Vuetify, integracja ze Store'ami).<br>• Przygotowanie testów jednostkowych, integracyjnych oraz weryfikacja poprawności działania przepływów.<br>• Prowadzenie dokumentacji technicznej, archiwizacja artefaktów projektowych oraz utrzymanie słowników *locale*. |
| **Maciej** | **Head Frontend Developer, Infrastructure Developer** | • Główna implementacja logiki interfejsu użytkownika w **Vue 3**, **Vuetify**, **VueFlow** oraz klientów **gRPC-Web**.<br>• Budowa i utrzymanie architektury kontenerowej (**Docker**, Docker Compose, **YARP Gateway**).<br>• Konfiguracja i wdrożenie instancji **Keycloak**, **PostgreSQL** oraz stosu monitorującego **Grafana**.<br>• Automatyzacja środowisk deweloperskich i procesów kompilacji plików `.proto`. |

---

## 🗺️ Plan Projektu (Roadmapa)

### Faza 1: Architektura i Infrastruktura Bazowa (do 24.10)
- [x] Przygotowanie środowiska uruchomieniowego `docker-compose` (PostgreSQL, Keycloak, Grafana, Gateway).
- [x] Konfiguracja Realmów i klientów OIDC w Keycloak.
- [x] Definicja CI/CD budowania serwisów i uruchamiania testów w GitHub Actions.
- [x] Implementacja gRPC-Web i Keycloak po stronie frontendu.
- [x] Dodanie magistrali eventów SignalR (`/events`).
- [x] Inicjalizacja szablonów mikroserwisów w .NET 11 oraz projektu bazowego Vue 3 + Vuetify + Pinia.
- [x] Zdefiniowanie wspólnego repozytorium kontraktów **Protocol Buffers (`.proto`)** oraz generatorów kodu dla C# i TypeScript.

### Faza 2: Rdzeń Backendowy i Silnik Agentów (Microsoft Agents AI + MCP) (do 7.11)
- [x] Implementacja warstwy danych w PostgreSQL (encje węzłów, krawędzi workflow, historii uruchomień).
- [x] Stworzenie mikroserwisu orkiestracji opartego o **Microsoft Agents AI** (`AgentEngine.cs`).
- [x] Implementacja dynamicznego konektora **MCP** (`McpClientService.cs`), pozwalającego na dynamiczne podpinanie narzędzi.
- [x] Obsługa wyboru modeli (OpenAI, Ollama, API niestandardowe), nadpisywania endpointów oraz wstrzykiwania promptów w czasie rzeczywistym.

### Faza 3: Warstwa Wizualna i Kreator VueFlow (do 21.11)
- [x] Stworzenie architektury Store'ów (Pinia) oraz ViewModeli zarządzających stanem kreatora i wywołaniami **gRPC-Web**.
- [x] Centralny moduł klienta gRPC (`src/clients/grpc.ts`) ze wsparciem tokenów Bearer.
- [x] Implementacja kanwy **VueFlow** wraz z niestandardowymi węzłami (Węzeł Agenta, Modelu, Promptu, Konektora MCP).
- [x] Budowa paneli konfiguracyjnych w Vuetify (`NodeConfigDialog.vue`) dla poszczególnych węzłów.
- [x] System powiadomień Snackbar/Toasts (`notifications.ts`, `AppNotificationSnackbar.vue`).
- [x] Pełna integracja systemu tłumaczeń (i18n: `pl.json`, `en.json`) oraz formatowania stref czasowych i logów (Luxon).

### Faza 4: Integracja, Bramka YARP, Telemetria i Testy (do 05.12)
- [x] Zbudowanie i skonfigurowanie bramki **Frontend Gateway (YARP Reverse Proxy)** na porcie `:5000`.
- [x] Spinanie warstwy frontendowej z mikroserwisami za pomocą **gRPC-Web** (wywołania unarne zapisu/odczytu oraz strumieniowanie odpowiedzi agentów przez SignalR).
- [x] Utworzenie dedykowanego projektu testowego **`AgentStudio.Service.Tests`** z testami integracyjnymi gRPC, nasłuchiwaniem na magistrali SignalR oraz wsparciem **Testcontainers (PostgreSQL)**.
- [x] Utworzenie testów jednostkowych silnika agentów `AgentEngineUnitTests`.
- [x] Wdrożenie testów frontendowych **Vitest + JSDOM** (`WorkflowE2E.spec.ts`, `NodeConfigDialog.spec.ts`, `grpc.spec.ts`, `notifications.spec.ts`).
- [x] Zintegrowanie automatycznych testów w GitHub Actions workflow.
- [ ] Dalsza rozbudowa dashboardów diagnostycznych w **Grafanie** w oparciu o metryki OpenTelemetry.

---

## ⚙️ Uruchomienie Środowiska Deweloperskiego

### Wymagania Wstępne
* **Docker** & **Docker Compose**
* **.NET 11 SDK**
* **Node.js** (v22+) & **npm / pnpm**

---

### Szybki Start (Całe Środowisko w Dockerze)

Aby uruchomić wszystkie mikroserwisy (Gateway, Backend Service, Frontend SPA, PostgreSQL, Keycloak, Grafana):

```bash
cd AgentStudio
docker compose up -d --build
```

Po uruchomieniu aplikacja dostępna jest pod adresami:
* 🌐 **AI Agent Studio (Aplikacja SPA przez Gateway):** [http://localhost:5000](http://localhost:5000)
* 🔐 **Keycloak IAM (Logowanie i Zarządzanie Realmami):** [http://localhost:8080](http://localhost:8080) (login: `admin` / hasło: `admin`)
* 📊 **Grafana Telemetria:** [http://localhost:3000](http://localhost:3000) (login: `admin` / hasło: `admin`)
* 🐘 **PostgreSQL:** `localhost:5432` (baza: `agents`, user: `keycloak`, hasło: `keycloak_password`)

---

### Uruchomienie Lokalne (Tryb Developerski)

1. **Uruchomienie baz danych i tożsamości:**
   ```bash
   cd AgentStudio
   docker compose up -d postgres keycloak
   ```

2. **Uruchomienie Backend Service:**
   ```bash
   dotnet run --project AgentStudio.Service
   # Serwis nasłuchuje na http://localhost:5001
   ```

3. **Uruchomienie Frontend Gateway (YARP):**
   ```bash
   dotnet run --project AgentStudio.Gateway
   # Bramka nasłuchuje na http://localhost:5000 i kieruje ruch do serwisu oraz klienta
   ```

4. **Uruchomienie Frontend Dev Server (Vite):**
   ```bash
   cd AgentStudio.Client
   npm install
   npm run dev
   # Interfejs deweloperski na http://localhost:5173
   ```

---

### Uruchamianie Testów

* **Testy integracyjne i jednostkowe backendu (.NET 11 + Testcontainers):**
  ```bash
  dotnet test AgentStudio/AgentStudio.Service.Tests/AgentStudio.Service.Tests.csproj
  ```
  *(Automatycznie uruchamia 8 testów pokrywających API gRPC, subskrypcje zdarzeń SignalR oraz orkiestrację silnika AgentEngine)*

* **Testy komponentów i E2E frontendu (Vitest + JSDOM):**
  ```bash
  cd AgentStudio/AgentStudio.Client
  npm test
  ```
  *(Automatycznie uruchamia 15 testów weryfikujących tworzenie węzłów, usuwanie powiązań, formularze konfiguracji modeli AI i powiadomienia)*
