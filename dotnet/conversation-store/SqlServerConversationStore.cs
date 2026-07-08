using Microsoft.Data.SqlClient;

namespace HeroWorkhorse.ConversationStore;

/// <summary>
/// SQL Server implementation. Two tables, plain SqlClient, no EF.
/// Call <see cref="EnsureCreatedAsync"/> once at startup to create the tables if missing.
/// </summary>
public sealed class SqlServerConversationStore(string connectionString) : IConversationStore
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID('dbo.Conversations') IS NULL
            CREATE TABLE dbo.Conversations (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                Title NVARCHAR(200) NULL,
                CreatedAt DATETIMEOFFSET NOT NULL
            );

            IF OBJECT_ID('dbo.Messages') IS NULL
            CREATE TABLE dbo.Messages (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                ConversationId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Conversations(Id),
                Role NVARCHAR(20) NOT NULL,
                Content NVARCHAR(MAX) NOT NULL,
                ToolCallsJson NVARCHAR(MAX) NULL,
                CreatedAt DATETIMEOFFSET NOT NULL,
                INDEX IX_Messages_ConversationId (ConversationId, CreatedAt)
            );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Conversation> CreateConversationAsync(string? title, CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation(Guid.NewGuid(), title, DateTimeOffset.UtcNow);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "INSERT INTO dbo.Conversations (Id, Title, CreatedAt) VALUES (@Id, @Title, @CreatedAt)", connection);
        command.Parameters.AddWithValue("@Id", conversation.Id);
        command.Parameters.AddWithValue("@Title", (object?)conversation.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", conversation.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return conversation;
    }

    public async Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT Id, Title, CreatedAt FROM dbo.Conversations WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new Conversation(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetDateTimeOffset(2));
    }

    public async Task AddMessagesAsync(Guid conversationId, IReadOnlyList<NewMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var message in messages)
        {
            await using var command = new SqlCommand(
                """
                INSERT INTO dbo.Messages (Id, ConversationId, Role, Content, ToolCallsJson, CreatedAt)
                VALUES (@Id, @ConversationId, @Role, @Content, @ToolCallsJson, @CreatedAt)
                """, connection, transaction);
            command.Parameters.AddWithValue("@Id", Guid.NewGuid());
            command.Parameters.AddWithValue("@ConversationId", conversationId);
            command.Parameters.AddWithValue("@Role", message.Role);
            command.Parameters.AddWithValue("@Content", message.Content);
            command.Parameters.AddWithValue("@ToolCallsJson", (object?)message.ToolCallsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT Id, ConversationId, Role, Content, ToolCallsJson, CreatedAt
            FROM dbo.Messages WHERE ConversationId = @ConversationId ORDER BY CreatedAt
            """, connection);
        command.Parameters.AddWithValue("@ConversationId", conversationId);

        var result = new List<StoredMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StoredMessage(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTimeOffset(5)));
        }
        return result;
    }
}
