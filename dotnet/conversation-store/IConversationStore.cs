namespace HeroWorkhorse.ConversationStore;

public sealed record Conversation(Guid Id, string? Title, DateTimeOffset CreatedAt);

/// <summary>
/// A stored message. Role is a free string ("user", "assistant", ...). ToolCallsJson optionally
/// carries the tool trace (arguments + results) of an assistant turn as an opaque JSON blob.
/// </summary>
public sealed record StoredMessage(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    string? ToolCallsJson,
    DateTimeOffset CreatedAt);

public sealed record NewMessage(string Role, string Content, string? ToolCallsJson = null);

/// <summary>
/// Persistence for conversations and their messages. Deliberately independent of
/// Microsoft.Extensions.AI types — map ChatMessage to/from NewMessage/StoredMessage at the edge.
/// </summary>
public interface IConversationStore
{
    Task<Conversation> CreateConversationAsync(string? title, CancellationToken cancellationToken = default);

    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddMessagesAsync(Guid conversationId, IReadOnlyList<NewMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Messages in chronological order.</summary>
    Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
