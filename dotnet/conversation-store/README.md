# dotnet/conversation-store

## What

Persistence for conversations and messages: an interface, a SQL Server implementation (plain `SqlClient`, no EF), and an in-memory fallback for dev/tests.

Decision on the spec's [OPEN] schema point — two tables, kept minimal:

- `Conversations` (Id, Title, CreatedAt)
- `Messages` (Id, ConversationId, Role, Content, ToolCallsJson, CreatedAt)

`ToolCallsJson` is an opaque JSON blob for an assistant turn's tool trace — stored for display/debugging, not replayed into the model. The store is deliberately independent of Microsoft.Extensions.AI types; map `ChatMessage` at the edge (see `samples/sql-agent/api/SqlAgentChat.cs` for a complete example).

## Files

- `IConversationStore.cs` — interface + `Conversation` / `StoredMessage` / `NewMessage` records.
- `SqlServerConversationStore.cs` — SQL Server implementation; `EnsureCreatedAsync()` creates the tables.
- `InMemoryConversationStore.cs` — dev fallback, lost on restart.

## Dependencies

- `Microsoft.Data.SqlClient`

## Usage

```csharp
var store = new SqlServerConversationStore(connectionString);
await store.EnsureCreatedAsync(); // once at startup

var conversation = await store.CreateConversationAsync(title: "First question…");
await store.AddMessagesAsync(conversation.Id,
[
    new NewMessage("user", "How many customers?"),
    new NewMessage("assistant", "There are 8 customers.", toolCallsJson),
]);
var history = await store.GetMessagesAsync(conversation.Id);
```

The agent-loop event model includes a `ConversationStarted` event: emit it as the first stream event when you create a new conversation, so the client learns the id to send on follow-up turns (the `angular/chat` block handles this automatically).

## Pairs with

- `dotnet/agent-loop` — persist the message history after each run.
- `dotnet/streaming` — `ChatRequest.ConversationId` carries the id between turns.
