# dotnet/streaming

## What

A minimal API endpoint that streams agent events to the browser: `POST /agents/{name}/chat` with body `{ "message": "...", "conversationId": null }`. Each Server-Sent Event's `data:` line is one JSON-serialized `AgentEvent` from the agent-loop block, camelCased with a `type` discriminator:

```
data: {"type":"tool-call-started","callId":"...","name":"run_query","argumentsJson":"..."}

data: {"type":"text-delta","text":"There are "}

data: {"type":"completed","stopReason":"done","steps":2,...}
```

Decision on the spec's [OPEN] point: **SSE**, not SignalR. The stream is strictly one-directional (client sends one POST, server streams events back), so SignalR's bidirectional connection management buys nothing here and would add a dependency on both sides. `TypedResults.ServerSentEvents` (built into .NET 10) plus a `fetch` reader on the Angular side is all that's needed. Note the client cannot use `EventSource` (it only does GET); the Angular chat block reads the POST response body as a stream instead.

**Swapping to SignalR** (if you need server-initiated pushes or multiplexing later): replace the endpoint with a hub method `IAsyncEnumerable<AgentEvent> Chat(ChatRequest request)` — SignalR supports streaming hub methods natively — and the Angular side with `@microsoft/signalr`'s `hubConnection.stream(...)`.

## Files

- `AgentChatEndpoint.cs` — `MapAgentChat()` extension + SSE serialization.
- `IAgent.cs` — the `IAgent` interface the endpoint routes to, and the `ChatRequest` record.

## Dependencies

- `dotnet/agent-loop` block (for `AgentEvent`)
- ASP.NET Core (framework reference)

## Usage

```csharp
// An agent = a name + something that turns a request into an event stream.
public sealed class SqlAgent(...) : IAgent
{
    public string Name => "sql";
    public IAsyncEnumerable<AgentEvent> RunAsync(ChatRequest request, CancellationToken ct)
    {
        // build messages, then delegate to AgentLoop.RunAsync(...)
    }
}

builder.Services.AddSingleton<IAgent, SqlAgent>();

var app = builder.Build();
app.MapAgentChat(); // POST /agents/{name}/chat
```

## Pairs with

- `dotnet/agent-loop` — produces the events this block streams.
- `angular/chat` — consumes this exact SSE format.
