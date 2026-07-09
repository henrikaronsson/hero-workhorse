# hero-workhorse

<p align="center">
  <img src="assets/hero.png" width="128" alt="hero-workhorse — copy-paste AI agent blocks for .NET + Angular and more ...">
</p>

A pragmatic, copy-paste AI toolbox, self-contained building blocks for adding LLM agents to real projects. Not a framework, not a package — just useful blocks to copy into a project.

- Each block is a handful of files with minimal dependencies and its own README (**What / Files / Dependencies / Usage / Pairs with**).
- No registry, no versioning, no breaking changes. Copy it, modify it, it's yours.
- Some of the target stack: Angular 20+, .NET 10, Docker, Azure, Azure SQL DB, PostgreSQL, etc.


## Quick start

Fastest way to see an agent run, fully local and free:

```bash
ollama pull gpt-oss:20b
dotnet run --project samples/smoke-test
```

This runs the `agent-loop` block against a local Ollama model and prints every agent event. It makes the model roll a die and then use that unpredictable result to pick which city's weather to look up — a dependent, multi-step chain that only works if an AI is genuinely reasoning in the loop. Prints `SMOKE TEST PASSED` when that chain completes cleanly. See [samples/smoke-test](samples/smoke-test/README.md).

## Blocks

| Block | What it gives you |
|---|---|
| [dotnet/agent-loop](dotnet/agent-loop/README.md) | The core agent while-loop on `Microsoft.Extensions.AI`: policies (max steps, timeout, token budget) + a typed event stream |
| [dotnet/sql-tools](dotnet/sql-tools/README.md) | `get_schema` + SELECT-only `run_query` agent tools for SQL Server, parser-validated |
| [dotnet/conversation-store](dotnet/conversation-store/README.md) | Conversation/message persistence: SQL Server implementation + in-memory fallback |
| [dotnet/streaming](dotnet/streaming/README.md) | `POST /agents/{name}/chat` minimal API endpoint streaming agent events as SSE |
| [angular/chat](angular/chat/README.md) | Chat component rendering streamed responses (standalone, signals, plain markup) |
| [angular/tool-trace](angular/tool-trace/README.md) | Collapsible per-response panel showing tool calls, arguments and results |
| [prompts/](prompts/README.md) | Proven system prompts (SQL agent, with schema-injection variant) |
| [infra/](infra/README.md) | Bicep (Container App + Key Vault + Managed Identity) and docker-compose pieces |
| [recipes/](recipes/) | Short wiring guides: [add a SQL agent to an existing app](recipes/add-sql-agent-to-existing-app.md), [swap Ollama for Azure OpenAI](recipes/swap-ollama-for-azure-openai.md) |

## How to copy a block

1. Copy the block's folder into your project (`.cs` files into a project of yours, Angular folders into `src/app/blocks/`).
2. Add the packages listed under **Dependencies** in its README.
3. Follow **Usage**, check **Pairs with** for blocks that complement it.

There is nothing to install from here — the repo's solution and sample exist only to develop and prove the blocks.

## See it run

[`samples/sql-agent`](samples/sql-agent/README.md) wires everything end-to-end: a .NET host and Angular UI where an agent answers questions about a seeded demo database, with live-streamed answers and visible tool traces. Runs fully locally (Ollama + dockerized SQL Server), no cloud keys. [`samples/smoke-test`](samples/smoke-test) is a minimal console harness that proves the agent loop with a multi-step, dice-driven tool chain.

## Conventions

- .NET 10, nullable enabled, file-scoped namespaces. Blocks log via `ILogger` only; the sample uses Serilog.
- Secrets: user secrets locally, Key Vault + Managed Identity on Azure. Never in appsettings (the sample's dockerized dev credentials are the one pragmatic exception).
- Providers are `IChatClient` implementations chosen in DI — blocks never talk to a specific LLM vendor.


