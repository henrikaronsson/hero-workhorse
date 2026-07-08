# dotnet/agent-loop

## What

The core agent while-loop on top of `Microsoft.Extensions.AI`: call the model, execute any requested tool calls, feed results back, repeat. Adds the policies a real app needs — max steps, wall-clock timeout, cancellation, optional token budget — and emits a stream of events you can pipe straight to a browser or a log.

Tool invocation is done inside the loop (instead of `FunctionInvokingChatClient`) precisely so every tool call and result can be surfaced as an event.

**Event model** (decided from the spec's [OPEN] point): a sealed record hierarchy with a JSON `type` discriminator, so it serializes cleanly for a TypeScript client:

| Event | `type` | Payload |
|---|---|---|
| `TextDelta` | `text-delta` | `text` |
| `ToolCallStarted` | `tool-call-started` | `callId`, `name`, `argumentsJson` |
| `ToolResult` | `tool-result` | `callId`, `name`, `resultJson`, `isError` |
| `Completed` | `completed` | `stopReason` (`done` / `max-steps` / `token-budget` / `cancelled`), `steps`, `inputTokens`, `outputTokens` |
| `AgentError` | `error` | `message` |

## Files

- `AgentLoop.cs` — the loop.
- `AgentEvent.cs` — event records.
- `AgentLoopOptions.cs` — policies.

## Dependencies

- `Microsoft.Extensions.AI.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

Bring your own `IChatClient` (OllamaSharp, Azure OpenAI, Anthropic, ...). The block never talks to a provider directly.

## Usage

```csharp
IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), "gpt-oss:20b");

var options = new ChatOptions
{
    Tools = [AIFunctionFactory.Create((string city) => "22 and sunny", "get_weather", "Current weather for a city.")],
};

var loop = new AgentLoop(chatClient, new AgentLoopOptions { MaxSteps = 10 });

List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Weather in Oslo?"),
];

await foreach (var evt in loop.RunAsync(messages, options))
{
    // switch on evt: TextDelta, ToolCallStarted, ToolResult, Completed, AgentError
}
// `messages` now contains the full history (assistant + tool messages appended).
```

Serialize events with System.Text.Json and the `type` discriminator comes along for free:

```csharp
JsonSerializer.Serialize<AgentEvent>(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
```

## Pairs with

- `dotnet/streaming` — pushes these events to the browser over SSE.
- `dotnet/sql-tools` — ready-made `AIFunction`s for the `Tools` list.
- `dotnet/conversation-store` — persist the `messages` list after the loop completes.
- `samples/smoke-test` — console harness proving the loop against local Ollama.
