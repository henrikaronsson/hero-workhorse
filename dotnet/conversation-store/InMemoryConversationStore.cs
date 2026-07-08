using System.Collections.Concurrent;

namespace HeroWorkhorse.ConversationStore;

/// <summary>Dev/test fallback. Everything is lost on restart.</summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, Conversation> _conversations = new();
    private readonly ConcurrentDictionary<Guid, List<StoredMessage>> _messages = new();

    public Task<Conversation> CreateConversationAsync(string? title, CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation(Guid.NewGuid(), title, DateTimeOffset.UtcNow);
        _conversations[conversation.Id] = conversation;
        _messages[conversation.Id] = [];
        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_conversations.TryGetValue(id, out var conversation) ? conversation : null);

    public Task AddMessagesAsync(Guid conversationId, IReadOnlyList<NewMessage> messages, CancellationToken cancellationToken = default)
    {
        var list = _messages.GetOrAdd(conversationId, _ => []);
        lock (list)
        {
            list.AddRange(messages.Select(m => new StoredMessage(
                Guid.NewGuid(), conversationId, m.Role, m.Content, m.ToolCallsJson, DateTimeOffset.UtcNow)));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!_messages.TryGetValue(conversationId, out var list))
        {
            return Task.FromResult<IReadOnlyList<StoredMessage>>([]);
        }
        lock (list)
        {
            return Task.FromResult<IReadOnlyList<StoredMessage>>([.. list]);
        }
    }
}
