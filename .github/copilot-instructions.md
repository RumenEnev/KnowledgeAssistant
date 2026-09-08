# Knowledge Assistant — Copilot Instructions

Knowledge Assistant is a RAG (Retrieval-Augmented Generation) app: an ASP.NET Core API backed by PostgreSQL+pgvector,
with an Angular web frontend and a WPF desktop client, both talking to the same API. Users chat with a locally hosted
(Ollama) or hosted (AdessoAiHub) model, grounded in ingested/chunked documents, and can register "tools" the model
can call during chat.

See `Documents/README.md` for the full architecture diagram and `Documents/rag-schema-setup.md` for the DB schema
(`rag.*` and `ai_interactions.*` PostgreSQL schemas).

## Solution layout (`KnowledgeAssistant.slnx`)

- `KnowledgeAssistant.Api` — ASP.NET Core Web API (net10.0). `Program.cs` wires up all DI, HTTP clients, CORS, and
  PostgreSQL/pgvector data source. Thin `Controllers/` delegate to Application services.
- `KnowledgeAssistant.Application` — service layer AND repository implementations. `Abstraction/` holds interfaces
  (`I*Repository`, `IModelGateway`, `IToolExecutor`, etc.); `Services/` holds both business-logic services
  (e.g. `ConversationService`) and repository implementations (e.g. `ConversationRepository`) side by side — repos are
  not in Infrastructure.
- `KnowledgeAssistant.Infrastructure` — external integrations: Ollama/AdessoAiHub model gateways (`OllamaModelGateway`,
  `AdessoAiHubModelGateway`), SSE streaming (`Streaming/`), tool execution (`Executors/LocalToolExecutor`,
  `ToolCallRegistry/`).
- `KnowledgeAssistant.Domain` — POCOs/entities only (`Conversation`, `Documents/*`, `ToolDefinitionEntity`, etc.), no
  logic.
- `KnowledgeAssistant.Contracts` — DTOs, enums, and cross-cutting constants shared with the API surface
  (`Dto/`, `Enums/`, `Definitions/` e.g. `SseEvents`, `ModelProviderNames`).
- `KnowledgeAssistant.Web` — Angular 17 SPA (chat UI, document/topic management, tool management, config).
- `KnowledgeAssistant.Wpf` — WPF desktop client mirroring tool management from the web app.
- `KnowledgeAssistant.Tools/*` — standalone tool projects (`DocumentCreator`, `TablesExtractor`, `FilesAnalyzer`)
  invocable by the assistant during chat.
- `KnowledgeAssistant.Support/RagEvaluation*` — offline evaluation harness for retrieval/LLM-judge quality, separate
  from the runtime app.

## Multi-provider model gateway pattern

The API supports two model providers side by side: **Ollama** (local) and **AdessoAiHub** (hosted). Each provider has
its own `HttpClient`-backed gateway class registered in `Program.cs` and exposed through a common interface
(`IModelGateway`/`INamedModelGateway` for chat, `INamedModelCatalogGateway` for listing models). `ModelGatewayResolver`
/`ModelProviderRegistry` pick the concrete gateway at runtime based on the provider name stored with a conversation
(`ModelProviderNames`). When adding a new provider, implement gateway + catalog-gateway classes, register both as
`INamedModelGateway`/`INamedModelCatalogGateway`, and add an HttpClient registration in `Program.cs` — do not special-case
providers in controllers or services.

## Tool-calling flow

Tools are NOT executed server-side in-process. `LocalToolExecutor` (Infrastructure/Executors) hands a tool call off to
the connected client over the SSE stream (`SseEvents.ToolCall`) and blocks on `IPendingToolCallRegistry.WaitForResultAsync`
(3-minute timeout) until the client (Angular/WPF) posts back a result. Keep this async hand-off pattern when adding new
tool types; don't run tool logic synchronously inside the API request unless you also update the client-side handler.

## Data access conventions

- Repositories use **Dapper + raw SQL** directly against `Npgsql`, not EF Core. Follow the existing pattern: open a
  `NpgsqlConnection` per call (`await using`), parameterized queries (`@Param`), explicit column aliasing to match
  C# property names (e.g. `c.created_at AS CreatedAt`).
  connection string comes from `IConfiguration.GetConnectionString("KnowledgeAssistant")` (repositories) or
  `builder.Configuration.GetConnectionString(...)` (Program.cs) — never hardcode connection strings.
- Tables live in two schemas: `ai_interactions.*` (conversations, chat_messages) and `rag.*` (documents, chunks, topics,
  embeddings via pgvector). Cross-schema joins are common (e.g. conversations LEFT JOIN rag.topics).
- Required configuration (`Ollama:BaseUrl`, `AdessoAiHub:BaseUrl`, `AdessoAiHub:ApiKey`, `ConnectionStrings:KnowledgeAssistant`,
  `Cors:AllowedOrigins`) is validated at startup in `Program.cs` and throws `InvalidOperationException` if missing —
  follow this fail-fast pattern for any new required config.

## Build & run

No CI workflows or automated test projects currently exist in this repo — validate changes by building/running locally.

- Backend: `dotnet build KnowledgeAssistant.slnx` from this directory; run the API with `dotnet run --project KnowledgeAssistant.Api`
  (F5 in Visual Studio also works). Requires a reachable PostgreSQL instance with `pgvector` and a reachable Ollama
  instance (see `appsettings.json` / `appsettings.Development.json` for local endpoints, `.env.example` for the
  Docker Compose equivalents).
- Frontend: from `KnowledgeAssistant.Web`, `npm install` then `npm start` (`ng serve`, http://localhost:4200) or
  `npm run build`. Unit tests: `npm test` (Karma/Jasmine) — to run a single spec, use
  `ng test --include='**/your-file.spec.ts'`.
- Docker: `docker-compose up` builds/runs `api` (port 5299) and `web` (port 4200) together; connection info is
  supplied via `KA_DB_CONNECTION_STRING` / `KA_OLLAMA_BASE_URL` env vars (see `.env.example`).
- CORS is pattern-based (`Cors:AllowedOrigins` supports `*` wildcards, matched via regex in `Program.cs`) — update that
  config, not code, when adding a new allowed origin.
