# Recipe: add a SQL agent to an existing .NET + Angular app

Wire `agent-loop` + `sql-tools` + `streaming` into a backend and `chat` + `tool-trace` into a frontend. ~30 minutes.

## Backend (.NET)

1. Copy `dotnet/agent-loop`, `dotnet/sql-tools` and `dotnet/streaming` into your solution (as projects or straight into an existing project — they're just files).
2. Add the packages each block's README lists, plus an `IChatClient` provider (`OllamaSharp` locally, `Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI` for Azure).
3. Create a read-only SQL login (SQL in the sql-tools README) and add its connection string.
4. Implement `IAgent` — the whole agent is ~40 lines; copy `samples/sql-agent/api/SqlAgentChat.cs` and adjust the system prompt (see `prompts/sql-agent.md`).
5. Register and map:

```csharp
builder.Services.AddSingleton<IChatClient>(_ => new OllamaApiClient(new Uri("http://localhost:11434"), "gpt-oss:20b"));
builder.Services.AddSingleton(new SqlTools(new SqlToolsOptions { ConnectionString = ... }));
builder.Services.AddSingleton<IAgent, MyAgent>();
app.MapAgentChat();
```

If the Angular app runs on another origin, add CORS for it.

## Frontend (Angular 20+)

1. Copy `angular/chat` and `angular/tool-trace` into `src/app/blocks/`.
2. Provide the API base URL: `{ provide: AGENT_API_BASE_URL, useValue: 'https://your-api' }`.
3. Drop `<hw-chat agentName="sql" />` wherever you want the chat. Restyle the `.scss` files to taste — the markup has no library dependencies.

## Optional: persistence

Copy `dotnet/conversation-store`, call `EnsureCreatedAsync()` at startup, and follow the pattern in `SqlAgentChat.cs`: load history before the loop, save the user + assistant messages after, and emit `ConversationStarted` for new conversations (the chat block picks the id up automatically).
