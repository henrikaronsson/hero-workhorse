# hero-workhorse

A pragmatic, copy-paste AI harness: a collection of self-contained building blocks for adding LLM agents to real projects. Not a framework, not a package — blocks you copy into a project and own.

**Note to the implementing AI (Cursor):** This spec is a starting point, not a contract. It's a pragmatic work in progress. If you see a better structure, a simpler abstraction, or a newer/more idiomatic library choice — improve it. Sections marked **[OPEN]** are intentionally undecided; make a sensible choice and document it in the block's README. Prefer simple and working over complete and clever.

## Philosophy (shadcn-style)

- Each block is **self-contained**: a handful of files, minimal dependencies.
- Blocks are **copied into projects, not installed** from a registry. No versioning, no breaking changes, free to modify per project.
- Every block has a short `README.md`: what it does, how to use it, which other blocks it pairs with.
- A `samples/` project wires blocks together end-to-end and doubles as the test bed.

## Target stack

Primary consumers are projects using: Angular 20+, .NET 10, Docker, Azure (App Service / Functions), SQL Server (sometimes Postgres, occasionally Cosmos DB).

## Repository structure

```
hero-workhorse/
├── dotnet/
│   ├── agent-loop/          # The core agent while-loop, built on Microsoft.Extensions.AI
│   ├── sql-tools/           # SELECT-only query tool + schema tool
│   ├── conversation-store/  # Conversation history persisted to SQL Server
│   └── streaming/           # SSE (or SignalR) endpoint streaming agent events
├── angular/
│   ├── chat/                # Chat component with streamed responses (signals, standalone)
│   └── tool-trace/          # Expandable per-response tool trace component
├── prompts/                 # Reusable system prompts / prompt templates
├── infra/                   # Bicep snippets + docker-compose pieces
├── recipes/                 # Short guides: "wire block X + Y into an app"
├── samples/
│   ├── smoke-test/          # Console harness: agent-loop + one toy tool against local Ollama
│   └── sql-agent/           # End-to-end: .NET host + Angular UI, agent answering DB questions
└── README.md                # Index of all blocks, philosophy, how to copy a block
```

## Blocks

### dotnet/agent-loop

- Built on **Microsoft.Extensions.AI** (`IChatClient`, `AIFunctionFactory`) — do NOT hand-roll a provider abstraction. Providers (Azure OpenAI, Anthropic, Ollama) are just `IChatClient` implementations chosen via appsettings.
- The block's own value is the loop + policies: max steps, timeout, cancellation, optional cost/token budget.
- Emits a stream of events (`TextDelta`, `ToolCallStarted`, `ToolResult`, `Completed`, `Error`) consumable by both the streaming block and logging.
- ~2–4 files. **[DECIDED]** event model: sealed record hierarchy with a System.Text.Json `type` discriminator (`text-delta`, `tool-call-started`, ...), mirrored 1:1 by a TypeScript union in `angular/chat`. A host-emitted `ConversationStarted` event carries new conversation ids to the client.
- Note: M.E.AI uses System.Text.Json for tool schemas. That's fine at the framework boundary; consuming projects may use Newtonsoft.Json in their own layers.

### dotnet/sql-tools

- `GetSchemaTool`: lists tables/columns/types.
- `QueryTool`: executes **SELECT only** — validate/parse before execution, enforce row limit and timeout, use a dedicated read-only connection string.
- SQL Server first. **[DECIDED]** SQL Server only in v1 (SELECT-only enforced by parsing with ScriptDom); Postgres/Cosmos were not cheap enough to add — noted as future work in the block README.

### dotnet/conversation-store

- Interface + SQL Server implementation for persisting conversations/messages. In-memory fallback for dev.
- **[DECIDED]** schema: `Conversations` (Id, Title, CreatedAt) + `Messages` (Id, ConversationId, Role, Content, ToolCallsJson, CreatedAt). Plain SqlClient, no EF; tables auto-created via `EnsureCreatedAsync()`.

### dotnet/streaming

- Minimal API endpoint: `POST /agents/{name}/chat`, streaming agent events to the client.
- Default to **SSE** for simplicity; note in README how to swap to SignalR. **[DECIDED]** SSE won: the stream is one-directional, .NET 10 has `TypedResults.ServerSentEvents` built in, and the Angular side only needs `fetch` + a stream reader (no EventSource — it can't POST).

### angular/chat + angular/tool-trace

- Angular 20+, standalone components, signals. No component-library dependency — plain, easily restyled markup.
- `chat`: message list + input, renders streamed text as it arrives.
- `tool-trace`: collapsed-by-default panel per assistant response showing which tools ran, arguments, results. Great for debugging and demos.

### prompts/

- Markdown files with proven system prompts (e.g. SQL agent prompt with schema-injection placeholder). Grows organically; starts with `sql-agent.md`.

### infra/

- Bicep snippet for hosting the sample (App Service or Container App) with Key Vault + Managed Identity for API keys.
- docker-compose pieces: host + SQL Server + Ollama for zero-cost local dev.

### samples/sql-agent

- Wires everything: agent-loop + sql-tools + conversation-store + streaming on the .NET side, chat + tool-trace on the Angular side, docker-compose to run locally against SQL Server with a seeded demo database.
- This is the acceptance test: if the sample runs and answers questions about the demo DB with visible tool traces, the harness works.

## Conventions

- .NET 10, nullable enabled, file-scoped namespaces. Serilog for logging in the sample; blocks themselves log via `ILogger` only.
- Secrets: user secrets locally, Key Vault + Managed Identity on Azure. Never in appsettings.
- Each block README follows the same template: **What / Files / Dependencies / Usage / Pairs with**.

## Non-goals (for now)

Multi-agent orchestration, vector index/RAG, auth (samples run behind your own gateway/nginx), package publishing.

## Suggested build order

1. `agent-loop` + a console smoke test against Ollama (free, local).
2. `sql-tools` + `streaming`, minimal API host.
3. `angular/chat` + `tool-trace`, wire the sample end-to-end with docker-compose.
4. `conversation-store`, `infra/`, polish READMEs and recipes.

Start with step 1, get it running, then iterate. Improve the spec as you go — update this file when reality wins.

## Status (2026-07-08)

All four steps built and verified: smoke test passes against local Ollama (`gpt-oss:20b`), and the sql-agent sample answers DB questions end-to-end with streamed text, visible tool traces, and conversation persistence (in-memory by default, SQL Server behind a config flag). Providers: OllamaSharp as the default `IChatClient`; Azure OpenAI swap documented in `recipes/`.
