using HeroWorkhorse.AgentLoop;
using Microsoft.Extensions.AI;
using OllamaSharp;

// Smoke test for the agent-loop block. It proves an AI is *genuinely* driving a multi-step loop,
// not just echoing a canned answer: the model rolls a die whose result it cannot predict, then has
// to use that result to decide which city's weather to look up, then reacts. A dependent second
// tool call built on an unpredictable first result is the tell-tale sign of real in-loop reasoning.
// Pass a model name as the first argument to override the default.

var model = args.Length > 0 ? args[0] : "gpt-oss:20b";
Console.WriteLine($"Smoke test against Ollama model '{model}'...\n");

IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434"), model);

var rng = new Random();

// Fictional destinations get gloriously unhelpful forecasts. The die decides which one fate picks.
var forecasts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Oslo"] = "4 degrees, sleet, and a northerly wind that openly judges your life choices",
    ["Honolulu"] = "28 degrees, sunny, a breeze smelling faintly of pineapple and smug",
    ["Mordor"] = "cloudy with a chance of ash; one does not simply pack sunscreen",
    ["Atlantis"] = "100% humidity, visibility nil, strongly recommend gills",
    ["the Moon"] = "-173 in the shade, +127 in the sun, zero wind, zero atmosphere, zero refunds",
    ["Hell, Michigan"] = "a mild 21 degrees, which is frankly disappointing given the name",
};

var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(
            (int sides) => rng.Next(1, sides + 1),
            "roll_dice",
            "Rolls a single die with the given number of sides and returns the result."),
        AIFunctionFactory.Create(
            (string city) => forecasts.TryGetValue(city, out var f) ? f : "22 degrees and suspiciously pleasant",
            "get_weather",
            "Gets the current weather for a city."),
    ],
};

var loop = new AgentLoop(chatClient, new AgentLoopOptions { MaxSteps = 6, Timeout = TimeSpan.FromMinutes(3) });

List<ChatMessage> messages =
[
    new(ChatRole.System,
        "You are the Oracle of Questionable Travel: a wildly over-dramatic fortune teller who sends " +
        "hapless mortals to absurd places. When asked to choose a destiny you MUST: " +
        "(1) roll a six-sided die using the roll_dice tool, " +
        "(2) map the roll to a destination — 1=Oslo, 2=Honolulu, 3=Mordor, 4=Atlantis, 5=the Moon, 6=Hell\\, Michigan, " +
        "(3) call get_weather for that exact destination, and only then " +
        "(4) pronounce the traveler's fate in ONE short, theatrical paragraph that names the destination, " +
        "quotes the weather, and rules on whether they will survive. Never skip the die or the weather check."),
    new(ChatRole.User, "Oracle! Roll for my destiny — where does fate hurl me, and will the weather finish me off?"),
];

var toolCalls = new List<string>();
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
            toolCalls.Add(t.Name);
            Console.WriteLine($"\n[tool call #{toolCalls.Count}] {t.Name}({t.ArgumentsJson})");
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

var rolled = toolCalls.Contains("roll_dice");
var checkedWeather = toolCalls.Contains("get_weather");
var multiStep = toolCalls.Count >= 2;

Console.WriteLine();
Console.WriteLine(
    $"dice rolled: {rolled}, weather checked: {checkedWeather}, multi-step: {multiStep}, " +
    $"text: {sawText}, completed: {completedOk}");

// The whole point: a dependent chain (roll -> pick city from the roll -> look up that city) can only
// happen if a model actually read the first tool result and reasoned about the next move.
if (rolled && checkedWeather && multiStep && sawText && completedOk)
{
    Console.WriteLine("SMOKE TEST PASSED — an AI really drove the loop.");
    return 0;
}

Console.WriteLine("SMOKE TEST FAILED");
return 1;
