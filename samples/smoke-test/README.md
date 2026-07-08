# samples/smoke-test

Minimal console harness for `dotnet/agent-loop`: one toy `get_weather` tool against a local Ollama model, printing every agent event. Exits 0 and prints `SMOKE TEST PASSED` when the model called the tool, streamed text, and completed cleanly.

```bash
ollama pull gpt-oss:20b
dotnet run --project samples/smoke-test          # default model
dotnet run --project samples/smoke-test llama3.3 # any tool-calling model
```
