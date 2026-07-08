# angular/tool-trace

## What

A collapsed-by-default panel shown under an assistant response, listing which tools ran, with what arguments, and what they returned (results pretty-printed, errors highlighted, in-flight calls marked "running"). Great for debugging and demos. Standalone component, signals, plain restylable markup.

## Files

- `tool-trace.component.ts` / `.html` / `.scss` — the component.
- `tool-call.ts` — the `ToolCall` view model (one entry per tool invocation).

## Dependencies

- Angular 20+. Nothing else.

## Usage

```typescript
import { ToolTraceComponent } from './tool-trace/tool-trace.component';
import { ToolCall } from './tool-trace/tool-call';
```

```html
<hw-tool-trace [toolCalls]="turn.toolCalls" />
```

Build the `ToolCall[]` from agent events: push an entry on `tool-call-started`, fill in `resultJson`/`isError` on `tool-result` (the `angular/chat` block does exactly this — see its `chat.component.ts`).

## Pairs with

- `angular/chat` — renders this under every assistant turn.
- `dotnet/agent-loop` — source of the tool call events.
