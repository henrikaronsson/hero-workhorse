# samples/sql-agent

End-to-end sample wiring all blocks together: a .NET host (agent-loop + sql-tools + streaming + conversation-store) and an Angular UI (chat + tool-trace), answering questions about a seeded demo database. This is the harness's acceptance test.

## Layout

- `api/` — .NET 10 minimal API host. `SqlAgentChat.cs` is the whole agent: system prompt + loop + tools.
- `ui/` — Angular 20 app. The `src/app/blocks/` folder contains verbatim copies of `angular/chat` and `angular/tool-trace` (blocks are copied, not referenced — that's the point).
- `db/seed.sql` — demo "Shop" database (customers, products, orders, order items) + a read-only `agent_reader` login.
- `docker-compose.yml` — SQL Server 2022 + a one-shot seed container.

## Run it

Prerequisites: .NET 10 SDK, Node 20+, Docker, and [Ollama](https://ollama.com) with a tool-calling model:

```bash
ollama pull gpt-oss:20b   # or any tool-calling model; set Ollama:Model in api/appsettings.json
```

Then, in three terminals from this folder:

```bash
docker compose up -d          # SQL Server on port 14330, seeds the Shop DB
dotnet run --project api      # API on http://localhost:5203
cd ui && npm install && npm start   # UI on http://localhost:4200
```

Open http://localhost:4200 and ask e.g. *"How many customers do we have per country?"*. You should see the answer stream in, with an expandable tool trace showing the `get_schema` and `run_query` calls.

No cloud keys needed — everything runs locally. To use Azure OpenAI instead, swap the `IChatClient` registration in `api/Program.cs`.

## Notes

- The SQL credentials in `appsettings.json` and `docker-compose.yml` are throwaway dev-only values for the local container. In a real project follow the conventions: user secrets locally, Key Vault + Managed Identity on Azure.
- The agent connects as `agent_reader` (db_datareader only), on top of the SELECT-only validation in sql-tools.
- Conversation persistence uses the in-memory store by default; set `ConversationStore:UseSqlServer` to `true` in `appsettings.json` to persist into the same SQL Server container (table creation is automatic on startup).
