# samples/smoke-test

Minimal console harness for `dotnet/agent-loop`: one toy `get_weather` tool against a local Ollama model, printing every agent event. Exits 0 and prints `SMOKE TEST PASSED` when the model called the tool, streamed text, and completed cleanly.

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
