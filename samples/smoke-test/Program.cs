using HeroWorkhorse.AgentLoop;
using Microsoft.Extensions.AI;
using OllamaSharp;

// Smoke test for the agent-loop block: run against a local Ollama model with one toy tool
// and print every event. Pass a model name as the first argument to override the default.

var model = args.Length > 0 ? args[0] : "gpt-oss:20b";
Console.WriteLine($"Smoke test against Ollama model '{model}'...\n");

IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), model);

var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(
            (string city) => city.Equals("oslo", StringComparison.OrdinalIgnoreCase)
                ? "4 degrees, sleet, wind from the north"
                : "22 degrees and sunny",
            "get_weather",
            "Gets the current weather for a city."),
    ],
};

var loop = new AgentLoop(chatClient, new AgentLoopOptions { MaxSteps = 5, Timeout = TimeSpan.FromMinutes(3) });

List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful assistant. Use the get_weather tool when asked about weather."),
    new(ChatRole.User, "What's the weather like in Oslo right now?"),
];

var sawToolCall = false;
var sawText = false;
var completedOk = false;

await foreach (var evt in loop.RunAsync(messages, chatOptions))
{
    switch (evt)
    {
        case TextDelta d:
            sawText = true;
            Console.Write(d.Text);
            break;
        case ToolCallStarted t:
            sawToolCall = true;
            Console.WriteLine($"\n[tool call] {t.Name}({t.ArgumentsJson})");
            break;
        case ToolResult r:
            Console.WriteLine($"[tool result] {r.ResultJson} (error: {r.IsError})");
            break;
        case Completed c:
            completedOk = c.StopReason == "done";
            Console.WriteLine($"\n\n[completed] reason={c.StopReason} steps={c.Steps} tokens={c.InputTokens}/{c.OutputTokens}");
            break;
        case AgentError e:
            Console.WriteLine($"\n[error] {e.Message}");
            break;
    }
}

Console.WriteLine();
Console.WriteLine($"tool call seen: {sawToolCall}, text seen: {sawText}, completed: {completedOk}");
if (sawToolCall && sawText && completedOk)
{
    Console.WriteLine("SMOKE TEST PASSED");
    return 0;
}

Console.WriteLine("SMOKE TEST FAILED");
return 1;
