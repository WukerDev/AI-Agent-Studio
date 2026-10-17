# AI Agent Studio – Wizualny Kreator i Orkiestrator Agentów AI

![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?style=flat-square&logo=dotnet)
![Vue.js](https://img.shields.io/badge/Vue.js-3.x-4FC08D?style=flat-square&logo=vuedotjs)
![Vuetify](https://img.shields.io/badge/Vuetify-3.x-1867C0?style=flat-square&logo=vuetify)
![gRPC](https://img.shields.io/badge/gRPC-gRPC--Web-244C5A?style=flat-square&logo=google)
![Protobuf](https://img.shields.io/badge/Protobuf-Protocol_Buffers-4285F4?style=flat-square&logo=google)
![Docker](https://img.shields.io/badge/Docker-Container-2496ED?style=flat-square&logo=docker)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-336791?style=flat-square&logo=postgresql)
![Keycloak](https://img.shields.io/badge/Keycloak-IAM-4D4D4D?style=flat-square&logo=keycloak)
![Grafana](https://img.shields.io/badge/Grafana-Observability-F46800?style=flat-square&logo=grafana)

## 📌 O Projekcie

**AI Agent Studio** to zaawansowana platforma oparta na architekturze mikroserwisowej, umożliwiająca wizualne projektowanie, konfigurowanie i uruchamianie autonomicznych oraz wieloagentowych systemów sztucznej inteligencji. 

Głównym założeniem systemu jest pełna separacja warstwy prezentacji od logiki wykonawczej. Użytkownik za pomocą interfejsu graficznego typu *node-based* (opartego o **VueFlow**) buduje przepływy pracy agentów, definiuje ich zachowanie, dobiera modele LLM oraz podpina zewnętrzne narzędzia z internetu za pomocą protokołu **MCP (Model Context Protocol)**. Całość egzekucji odbywa się w wysoko wydajnym środowisku backendowym **.NET 11** z wykorzystaniem ekosystemu **Microsoft AI Agents**.

---

## 🚀 Główne Funkcjonalności

* **Wizualny Kreator Przepływów (VueFlow):** Projektowanie architektury agentów oraz ich wzajemnych relacji metodą *drag-and-drop*.
* **Dynamiczna Konfiguracja Agentów:**
  * **Wybór Modelu:** Wsparcie dla różnych dostawców i modeli LLM (lokalnych oraz chmurowych).
  * **Zarządzanie Promptami:** Definiowanie ról systemowych, instrukcji bazowych oraz kontekstu.
  * **Konfiguracja Endpointów:** Elastyczne podpinanie niestandardowych adresów API oraz parametrów inferencji.
* **Integracja z MCP (Model Context Protocol):** Dynamiczne podłączanie zewnętrznych serwerów MCP z internetu, dające agentom dostęp do narzędzi, baz wiedzy i zewnętrznych API w czasie rzeczywistym.
* **Komunikacja Wysokiej Wydajności (gRPC & gRPC-Web):** Silnie typowana komunikacja oparta na kontraktach **Protocol Buffers (Protobuf)** – zarówno pomiędzy mikroserwisami backendowymi (**gRPC**), jak i bezpośrednio na linii przeglądarka – serwer (**gRPC-Web**).
* **Bezpieczeństwo i Autoryzacja:** Centralne zarządzanie tożsamością, rolami oraz tokenami dostępowymi za pomocą **Keycloak** (OIDC / OAuth2).
* **Pełna Obserwowalność:** Monitorowanie metryk wykonania, czasu odpowiedzi węzłów, zużycia tokenów oraz logów systemowych w czasie rzeczywistym na dashboardach **Grafany**.

---

## 🛠️ Stos Technologiczny

### Frontend
* **Framework:** Vue 3 (Composition API, TypeScript)
* **Zarządzanie Stanem i Architektura:** Pinia (separacja logiki biznesowej do dedykowanych ViewModeli i Store'ów)
* **UI Framework:** Vuetify 3
* **Silnik Grafowy:** VueFlow
* **Komunikacja z API:** gRPC-Web & Protocol Buffers (generowane klienty TypeScript)
* **Internacjonalizacja:** Pełne wsparcie słowników językowych (i18n)
* **Zarządzanie Czasem:** Luxon

### Backend & AI
* **Środowisko:** .NET 11 (C#)
* **Orkiestracja AI:** Microsoft AI Agents Framework (MS Agents AI / Semantic Kernel)
* **Integracja Narzędzi:** Model Context Protocol (MCP SDK)
* **Komunikacja i Kontrakty:** gRPC, gRPC-Web Middleware & Protocol Buffers (`.proto`)
* **Baza Danych:** PostgreSQL

### Infrastruktura & DevOps
* **Konteneryzacja:** Docker & Docker Compose
* **Tożsamość i Dostęp (IAM):** Keycloak
* **Monitoring i Telemetria:** Grafana (OpenTelemetry / Prometheus)

---

## 👥 Zespół Projektowy i Podział Zadań

| Członek Zespołu | Role w Projekcie | Główne Obszary Odpowiedzialności |
| :--- | :--- | :--- |
| **Wiktor** | **Project Leader, Head Designer** | • Nadzór nad architekturą systemu i kierunkiem rozwoju projektu.<br>• Projektowanie UI/UX, architektury komponentów oraz przepływów w VueFlow.<br>• Definiowanie standardów kodowania (architektura *top-to-bottom*, separacja ViewModeli i Store'ów).<br>• Projektowanie kontraktów **Protobuf** oraz integracji **gRPC-Web** na styku Frontend – Backend. |
| **Miłosz** | **Head Backend Developer, Microservice Developer** | • Projektowanie i implementacja mikroserwisów w oparciu o **.NET 11**.<br>• Tworzenie usług **gRPC**, obsługa warstwy **gRPC-Web** oraz definicji kontraktów **Protocol Buffers**.<br>• Projektowanie schematów bazy danych **PostgreSQL** oraz warstwy persystencji.<br>• Integracja backendu z serwerem autoryzacji **Keycloak**. |
| **Sebastian** | **Archiwista, Tester, AI Agents Developer, Co-Frontend Developer** | • Implementacja logiki agentów przy użyciu **MS Agents AI** oraz klienta **MCP**.<br>• Wsparcie rozwoju warstwy frontendowej (widoki, komponenty Vuetify, integracja ze Store'ami).<br>• Przygotowanie testów jednostkowych, integracyjnych oraz weryfikacja poprawności działania przepływów.<br>• Prowadzenie dokumentacji technicznej, archiwizacja artefaktów projektowych oraz utrzymanie słowników *locale*. |
| **Maciej** | **Head Frontend Developer, Infrastructure Developer** | • Główna implementacja logiki interfejsu użytkownika w **Vue 3**, **Vuetify**, **VueFlow** oraz klientów **gRPC-Web**.<br>• Budowa i utrzymanie architektury kontenerowej (**Docker**, Docker Compose).<br>• Konfiguracja i wdrożenie instancji **Keycloak**, **PostgreSQL** oraz stosu monitorującego **Grafana**.<br>• Automatyzacja środowisk deweloperskich i procesów kompilacji plików `.proto`. |

---

## 🗺️ Plan Projektu (Roadmapa)

### Faza 1: Architektura i Infrastruktura Bazowa
- [ ] Przygotowanie środowiska uruchomieniowego `docker-compose` (PostgreSQL, Keycloak, Grafana).
- [ ] Konfiguracja Realmów i klientów OIDC w Keycloak.
- [ ] Inicjalizacja szablonów mikroserwisów w .NET 11 oraz projektu bazowego Vue 3 + Vuetify + Pinia.
- [ ] Zdefiniowanie wspólnego repozytorium kontraktów **Protocol Buffers (`.proto`)** oraz skonfigurowanie generatorów kodu dla C# (.NET 11) i TypeScript (**gRPC-Web**).

### Faza 2: Rdzeń Backendowy i Silnik Agentów (MS Agents AI + MCP)
- [ ] Implementacja warstwy danych w PostgreSQL (zapis konfiguracji agentów, historii uruchomień, definicji grafów).
- [ ] Stworzenie mikroserwisu orkiestracji opartego o **MS Agents AI**.
- [ ] Implementacja dynamicznego konektora **MCP**, pozwalającego na podpinanie zewnętrznych serwerów narzędziowych z internetu.
- [ ] Obsługa wyboru modeli, nadpisywania endpointów oraz wstrzykiwania promptów systemowych w czasie rzeczywistym.

### Faza 3: Warstwa Wizualna i Kreator VueFlow
- [ ] Stworzenie architektury Store'ów (Pinia) oraz ViewModeli zarządzających stanem kreatora i wywołaniami **gRPC-Web**.
- [ ] Implementacja kanwy **VueFlow** wraz z niestandardowymi węzłami (Węzeł Agenta, Węzeł Modelu, Węzeł Promptu, Konektor MCP).
- [ ] Budowa paneli konfiguracyjnych w Vuetify dla poszczególnych węzłów.
- [ ] Pełna integracja systemu tłumaczeń (i18n) oraz formatowania stref czasowych i logów (Luxon).

### Faza 4: Integracja, Telemetria i Testy
- [ ] Spinanie warstwy frontendowej z mikroserwisami za pomocą **gRPC-Web** (obsługa wywołań unarnych oraz streamingu odpowiedzi agentów).
- [ ] Wdrożenie eksporterów OpenTelemetry w .NET 11 i budowa dashboardów diagnostycznych w **Grafanie**.
- [ ] Testy E2E kreatora, testy obciążeniowe komunikacji gRPC / Protobuf oraz weryfikacja bezpieczeństwa połączeń MCP.
- [ ] Kompletacja dokumentacji technicznej i archiwizacja specyfikacji projektu.

---

## ⚙️ Uruchomienie Środowiska Deweloperskiego

### Wymagania wstępne
* **Docker** & **Docker Compose**
* **.NET 11 SDK**
* **Node.js** (v22+) & **pnpm / npm**
* **Protobuf Compiler (`protoc`)**

### Szybki start (Infrastruktura)

```bash
git clone [https://github.com/twoja-organizacja/ai-agent-studio.git](https://github.com/twoja-organizacja/ai-agent-studio.git)
cd ai-agent-studio
docker compose up -d
