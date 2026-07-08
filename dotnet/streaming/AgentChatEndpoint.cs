using System.Runtime.CompilerServices;
using System.Text.Json;
using HeroWorkhorse.AgentLoop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HeroWorkhorse.Streaming;

public static class AgentChatEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps POST /agents/{name}/chat, streaming the agent's events as Server-Sent Events.
    /// Each SSE data line is one JSON-serialized <see cref="AgentEvent"/> with a "type" discriminator.
    /// </summary>
    public static IEndpointConventionBuilder MapAgentChat(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/agents/{name}/chat", IResult (
            string name,
            ChatRequest request,
            IEnumerable<IAgent> agents,
            CancellationToken cancellationToken) =>
        {
            var agent = agents.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (agent is null)
            {
                return TypedResults.NotFound($"Unknown agent '{name}'.");
            }

            return TypedResults.ServerSentEvents(SerializeEvents(agent.RunAsync(request, cancellationToken), cancellationToken));
        });
    }

    private static async IAsyncEnumerable<string> SerializeEvents(
        IAsyncEnumerable<AgentEvent> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            yield return JsonSerializer.Serialize(evt, JsonOptions);
        }
    }
}
