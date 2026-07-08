using HeroWorkhorse.AgentLoop;
using Microsoft.Extensions.AI;

namespace HeroWorkhorse.Tests;

public class AgentLoopTests
{
    private static List<ChatMessage> UserMessage(string text = "hi") =>
        [new ChatMessage(ChatRole.User, text)];

    private static async Task<List<AgentEvent>> RunAsync(AgentLoop.AgentLoop loop, IList<ChatMessage> messages, ChatOptions? options = null)
    {
        var events = new List<AgentEvent>();
        await foreach (var e in loop.RunAsync(messages, options))
        {
            events.Add(e);
        }
        return events;
    }

    [Fact]
    public async Task Plain_text_answer_streams_deltas_and_completes_done()
    {
        var client = new FakeChatClient().AddTextTurn("Hello", " world");
        var loop = new AgentLoop.AgentLoop(client);
        var messages = UserMessage();

        var events = await RunAsync(loop, messages);

        Assert.Equal(["Hello", " world"], events.OfType<TextDelta>().Select(d => d.Text));
        var completed = Assert.IsType<Completed>(events[^1]);
        Assert.Equal("done", completed.StopReason);
        Assert.Equal(1, completed.Steps);
        Assert.Equal(2, messages.Count); // user + assistant appended
    }

    [Fact]
    public async Task Tool_call_invokes_tool_and_feeds_result_back()
    {
        var toolInvoked = false;
        var tool = AIFunctionFactory.Create((string city) =>
        {
            toolInvoked = true;
            return $"Sunny in {city}";
        }, "get_weather");

        var client = new FakeChatClient()
            .AddToolCallTurn("call-1", "get_weather", new Dictionary<string, object?> { ["city"] = "Oslo" })
            .AddTextTurn("It is sunny.");
        var loop = new AgentLoop.AgentLoop(client);
        var messages = UserMessage("weather in Oslo?");

        var events = await RunAsync(loop, messages, new ChatOptions { Tools = [tool] });

        Assert.True(toolInvoked);
        var started = Assert.Single(events.OfType<ToolCallStarted>());
        Assert.Equal("get_weather", started.Name);
        var result = Assert.Single(events.OfType<ToolResult>());
        Assert.False(result.IsError);
        Assert.Contains("Sunny in Oslo", result.ResultJson);
        Assert.Equal("done", Assert.IsType<Completed>(events[^1]).StopReason);
        Assert.Equal(2, client.CallCount);
        // Tool result message must be in history so the model saw it on the second call.
        Assert.Contains(messages, m => m.Role == ChatRole.Tool);
    }

    [Fact]
    public async Task Unknown_tool_yields_error_result_instead_of_crashing()
    {
        var client = new FakeChatClient()
            .AddToolCallTurn("call-1", "no_such_tool")
            .AddTextTurn("Sorry.");
        var loop = new AgentLoop.AgentLoop(client);

        var events = await RunAsync(loop, UserMessage());

        var result = Assert.Single(events.OfType<ToolResult>());
        Assert.True(result.IsError);
        Assert.Equal("done", Assert.IsType<Completed>(events[^1]).StopReason);
    }

    [Fact]
    public async Task Throwing_tool_yields_error_result_and_loop_continues()
    {
        var tool = AIFunctionFactory.Create(
            new Func<string>(() => throw new InvalidOperationException("boom")), "explode");
        var client = new FakeChatClient()
            .AddToolCallTurn("call-1", "explode")
            .AddTextTurn("The tool failed.");
        var loop = new AgentLoop.AgentLoop(client);

        var events = await RunAsync(loop, UserMessage(), new ChatOptions { Tools = [tool] });

        var result = Assert.Single(events.OfType<ToolResult>());
        Assert.True(result.IsError);
        Assert.Contains("boom", result.ResultJson);
        Assert.Equal("done", Assert.IsType<Completed>(events[^1]).StopReason);
    }

    [Fact]
    public async Task Max_steps_stops_a_tool_calling_model()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "ping");
        var client = new FakeChatClient();
        for (var i = 0; i < 10; i++)
        {
            client.AddToolCallTurn($"call-{i}", "ping");
        }
        var loop = new AgentLoop.AgentLoop(client, new AgentLoopOptions { MaxSteps = 3 });

        var events = await RunAsync(loop, UserMessage(), new ChatOptions { Tools = [tool] });

        var completed = Assert.IsType<Completed>(events[^1]);
        Assert.Equal("max-steps", completed.StopReason);
        Assert.Equal(3, completed.Steps);
        Assert.Equal(3, client.CallCount);
    }

    [Fact]
    public async Task Timeout_completes_with_cancelled()
    {
        var client = new FakeChatClient { DelayPerUpdate = TimeSpan.FromSeconds(5) }
            .AddTextTurn("too", " slow");
        var loop = new AgentLoop.AgentLoop(client, new AgentLoopOptions
        {
            Timeout = TimeSpan.FromMilliseconds(100),
        });

        var events = await RunAsync(loop, UserMessage());

        var completed = Assert.IsType<Completed>(events[^1]);
        Assert.Equal("cancelled", completed.StopReason);
    }

    [Fact]
    public async Task Token_budget_stops_the_loop()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "ping");
        var usage = new UsageDetails { InputTokenCount = 900, OutputTokenCount = 200 };
        var client = new FakeChatClient()
            .AddToolCallTurn("call-1", "ping", usage: usage)
            .AddToolCallTurn("call-2", "ping", usage: usage);
        var loop = new AgentLoop.AgentLoop(client, new AgentLoopOptions { MaxTotalTokens = 1000 });

        var events = await RunAsync(loop, UserMessage(), new ChatOptions { Tools = [tool] });

        var completed = Assert.IsType<Completed>(events[^1]);
        Assert.Equal("token-budget", completed.StopReason);
        Assert.Equal(1, completed.Steps);
        Assert.Equal(900, completed.InputTokens);
        Assert.Equal(200, completed.OutputTokens);
    }

    [Fact]
    public async Task Model_failure_emits_error_and_stops()
    {
        var client = new FakeChatClient { ThrowOnNextCall = new HttpRequestException("connection refused") };
        var loop = new AgentLoop.AgentLoop(client);

        var events = await RunAsync(loop, UserMessage());

        var error = Assert.IsType<AgentError>(events[^1]);
        Assert.Contains("connection refused", error.Message);
        Assert.DoesNotContain(events, e => e is Completed);
    }

    [Fact]
    public async Task External_cancellation_completes_with_cancelled()
    {
        var client = new FakeChatClient { DelayPerUpdate = TimeSpan.FromSeconds(5) }
            .AddTextTurn("never", " finishes");
        var loop = new AgentLoop.AgentLoop(client);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var events = new List<AgentEvent>();
        await foreach (var e in loop.RunAsync(UserMessage(), null, cts.Token))
        {
            events.Add(e);
        }

        var completed = Assert.IsType<Completed>(events[^1]);
        Assert.Equal("cancelled", completed.StopReason);
    }
}
