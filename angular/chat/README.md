# angular/chat

## What

A chat UI for agents: message list + input, rendering streamed responses token by token. Standalone component, signals, plain restylable markup (no component library). Consumes the exact SSE format produced by `dotnet/streaming` — each `data:` line is one `AgentEvent` JSON object.

The service uses `fetch` with a stream reader rather than `EventSource`, because the endpoint is a POST with a JSON body (EventSource only supports GET).

## Files

- `chat.component.ts` / `.html` / `.scss` — the component.
- `agent-chat.service.ts` — POSTs to `/agents/{name}/chat` and parses the SSE stream into typed events.
- `agent-events.ts` — TypeScript mirror of the .NET `AgentEvent` model.

## Dependencies

- Angular 20+, RxJS. Nothing else.
- Imports `ToolTraceComponent` from the `angular/tool-trace` block to show per-response tool traces. If you copy chat without tool-trace, delete the `<hw-tool-trace>` element and its import — everything else works unchanged.

## Usage

```typescript
// app config: point the service at your .NET host (omit if same-origin)
{ provide: AGENT_API_BASE_URL, useValue: 'http://localhost:5214' }
```

```html
<hw-chat agentName="sql" placeholder="Ask about the database…" />
```

The component keeps its own conversation state in signals. For multi-conversation apps, lift `turns` into your own service and use `AgentChatService.streamChat(...)` directly — it is deliberately UI-free.

## Pairs with

- `dotnet/streaming` — the endpoint this talks to.
- `angular/tool-trace` — per-response tool call panel.
