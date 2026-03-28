using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Conversations;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresConversationRepository : IConversationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresConversationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO conversations (id, workspace_id, title, created_at_utc, updated_at_utc)
                           VALUES (@id, @workspaceId, @title, @createdAtUtc, @updatedAtUtc);
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", conversation.Id);
        command.Parameters.AddWithValue("workspaceId", conversation.WorkspaceId);
        command.Parameters.AddWithValue("title", conversation.Title);
        command.Parameters.AddWithValue("createdAtUtc", conversation.CreatedAtUtc);
        command.Parameters.AddWithValue("updatedAtUtc", conversation.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE conversations
                           SET title = @title,
                               updated_at_utc = @updatedAtUtc
                           WHERE id = @id
                             AND workspace_id = @workspaceId;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", conversation.Id);
        command.Parameters.AddWithValue("workspaceId", conversation.WorkspaceId);
        command.Parameters.AddWithValue("title", conversation.Title);
        command.Parameters.AddWithValue("updatedAtUtc", conversation.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO conversation_messages (
                               id,
                               conversation_id,
                               role,
                               content,
                               model_name,
                               created_at_utc
                           )
                           VALUES (
                               @id,
                               @conversationId,
                               @role,
                               @content,
                               @modelName,
                               @createdAtUtc
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", message.Id);
        command.Parameters.AddWithValue("conversationId", message.ConversationId);
        command.Parameters.AddWithValue("role", message.Role.ToString());
        command.Parameters.AddWithValue("content", message.Content);
        command.Parameters.AddWithValue("modelName", (object?)message.ModelName ?? DBNull.Value);
        command.Parameters.AddWithValue("createdAtUtc", message.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Conversation?> GetByIdAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id, workspace_id, title, created_at_utc, updated_at_utc
                           FROM conversations
                           WHERE workspace_id = @workspaceId
                             AND id = @conversationId;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("conversationId", conversationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var conversation = new Conversation(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTime>(3));

        conversation.Touch(reader.GetFieldValue<DateTime>(4));
        return conversation;
    }

    public async Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT m.id,
                                  m.conversation_id,
                                  m.role,
                                  m.content,
                                  m.model_name,
                                  m.created_at_utc
                           FROM conversation_messages AS m
                           INNER JOIN conversations AS c ON c.id = m.conversation_id
                           WHERE c.workspace_id = @workspaceId
                             AND m.conversation_id = @conversationId
                           ORDER BY m.created_at_utc, m.id;
                           """;

        var messages = new List<ConversationMessage>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("conversationId", conversationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new ConversationMessage(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<MessageRole>(reader.GetString(2), ignoreCase: true),
                reader.GetString(3),
                reader.GetFieldValue<DateTime>(5),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return messages;
    }
}
