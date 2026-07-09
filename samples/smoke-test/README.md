# samples/smoke-test

Minimal console harness for `dotnet/agent-loop`, printing every agent event. The "Oracle of Questionable Travel" persona rolls a `roll_dice` die, uses the (unpredictable) result to pick a destination, then calls `get_weather` for that place and pronounces your fate. That dependent chain — a second tool call built on a random first result — is the point: it can only happen if an AI actually read the roll and reasoned about the next move, so a pass means the model genuinely drove the loop rather than echoing a canned answer. Exits 0 and prints `SMOKE TEST PASSED` when the model rolled the die, checked the weather, ran a multi-step loop, streamed text, and completed cleanly.

## Prerequisites

- .NET 10 SDK
- [Ollama](https://ollama.com) running locally (default `http://localhost:11434`) — on Windows/macOS a host install usually gets better GPU access than the container in `infra/docker-compose/ollama.yml`
- A tool-calling model pulled (`gpt-oss:20b` by default; smaller models like `llama3.2` also work)

## Run

```bash
ollama pull gpt-oss:20b
dotnet run --project samples/smoke-test          # default model
dotnet run --project samples/smoke-test llama3.3 # any tool-calling model
```

If the model skips the tool or emits malformed calls, that is usually the model rather than the loop — try a different tool-calling model before debugging the code.
