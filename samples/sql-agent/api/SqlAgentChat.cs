using System.Runtime.CompilerServices;
using System.Text.Json;
using HeroWorkhorse.AgentLoop;
using HeroWorkhorse.ConversationStore;
using HeroWorkhorse.SqlTools;
using HeroWorkhorse.Streaming;
using Microsoft.Extensions.AI;

namespace SqlAgent.Api;

/// <summary>The "sql" agent: agent-loop + sql-tools + conversation-store behind the streaming endpoint.</summary>
public sealed class SqlAgentChat(
    IChatClient chatClient,
    SqlTools sqlTools,
    IConversationStore conversationStore,
    ILogger<AgentLoop> loopLogger) : IAgent
{
    private const string SystemPrompt = """
        You are a helpful data analyst with read-only access to a SQL Server database.

        Rules:
        - Call get_schema first if you are not sure which tables or columns exist.
        - Use run_query with a single SELECT statement to fetch data. Only SELECT is allowed.
        - If a query fails, read the error, fix the query and retry.
        - Answer the user's question concisely in plain language, based on the query results.
        - Never invent data; if the database cannot answer the question, say so.
        """;

    public string Name => "sql";

    public async IAsyncEnumerable<AgentEvent> RunAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt)];

        Guid conversationId;
        if (Guid.TryParse(request.ConversationId, out var existingId)
            && await conversationStore.GetConversationAsync(existingId, cancellationToken) is not null)
        {
            conversationId = existingId;
            // Replay prior user/assistant text; tool payloads stay in the store for display only.
            foreach (var stored in await conversationStore.GetMessagesAsync(conversationId, cancellationToken))
            {
                messages.Add(new ChatMessage(
                    stored.Role == "user" ? ChatRole.User : ChatRole.Assistant, stored.Content));
            }
        }
        else
        {
            var conversation = await conversationStore.CreateConversationAsync(
                request.Message.Length > 80 ? request.Message[..80] : request.Message, cancellationToken);
            conversationId = conversation.Id;
            yield return new ConversationStarted(conversationId.ToString());
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Message));

        var loop = new AgentLoop(
            chatClient,
            new AgentLoopOptions { MaxSteps = 8, Timeout = TimeSpan.FromMinutes(3) },
            loopLogger);
        var chatOptions = new ChatOptions { Tools = [.. sqlTools.AsAIFunctions()] };

        var assistantText = new System.Text.StringBuilder();
        var toolCalls = new List<object>();

        await foreach (var evt in loop.RunAsync(messages, chatOptions, cancellationToken))
        {
            switch (evt)
            {
                case TextDelta delta:
                    assistantText.Append(delta.Text);
                    break;
                case ToolResult result:
                    toolCalls.Add(new { result.CallId, result.Name, result.ResultJson, result.IsError });
                    break;
            }
            yield return evt;
        }

        await conversationStore.AddMessagesAsync(conversationId,
        [
            new NewMessage("user", request.Message),
            new NewMessage("assistant", assistantText.ToString(),
                toolCalls.Count > 0 ? JsonSerializer.Serialize(toolCalls) : null),
        ], CancellationToken.None);
    }
}
