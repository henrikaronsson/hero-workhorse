using HeroWorkhorse.AgentLoop;

namespace HeroWorkhorse.Streaming;

/// <summary>
/// A named agent the chat endpoint can route to. Register implementations in DI;
/// the endpoint resolves them by <see cref="Name"/> from the URL.
/// </summary>
public interface IAgent
{
    string Name { get; }

    IAsyncEnumerable<AgentEvent> RunAsync(ChatRequest request, CancellationToken cancellationToken);
}

/// <summary>Body of POST /agents/{name}/chat.</summary>
public sealed record ChatRequest(string Message, string? ConversationId);
