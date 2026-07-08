using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace HeroWorkhorse.Tests;

/// <summary>
/// Scripted IChatClient: each call to GetStreamingResponseAsync yields the next
/// pre-recorded turn. Supports an optional per-update delay to exercise timeouts.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly Queue<IReadOnlyList<ChatResponseUpdate>> _turns = new();

    public TimeSpan DelayPerUpdate { get; set; } = TimeSpan.Zero;
    public Exception? ThrowOnNextCall { get; set; }
    public int CallCount { get; private set; }

    public FakeChatClient AddTextTurn(params string[] chunks)
    {
        var updates = chunks
            .Select(c => new ChatResponseUpdate(ChatRole.Assistant, c))
            .ToList();
        _turns.Enqueue(updates);
        return this;
    }

    public FakeChatClient AddToolCallTurn(string callId, string toolName, IDictionary<string, object?>? arguments = null, UsageDetails? usage = null)
    {
        var update = new ChatResponseUpdate(
            ChatRole.Assistant,
            [new FunctionCallContent(callId, toolName, arguments)]);
        var updates = new List<ChatResponseUpdate> { update };
        if (usage is not null)
        {
            updates.Add(new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]));
        }
        _turns.Enqueue(updates);
        return this;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ThrowOnNextCall is { } ex)
        {
            ThrowOnNextCall = null;
            throw ex;
        }

        var turn = _turns.Count > 0
            ? _turns.Dequeue()
            : [new ChatResponseUpdate(ChatRole.Assistant, "(out of scripted turns)")];

        foreach (var update in turn)
        {
            if (DelayPerUpdate > TimeSpan.Zero)
            {
                await Task.Delay(DelayPerUpdate, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("AgentLoop only uses the streaming API.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
