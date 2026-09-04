using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed record LegalChatConversation(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int MessageCount, string? LastMessage);
public sealed record LegalChatMessage(Guid Id, Guid ConversationId, string Role, string Content, string Provider, DateTimeOffset CreatedAt);
public sealed record LegalChatExchange(LegalChatConversation Conversation, LegalChatMessage UserMessage, LegalChatMessage AssistantMessage);

public sealed class PostgresLegalChatStore(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS legal_chat_conversations (
          id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
          user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
          title TEXT NOT NULL,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
          updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS legal_chat_messages (
          id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
          conversation_id UUID NOT NULL REFERENCES legal_chat_conversations(id) ON DELETE CASCADE,
          user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
          role TEXT NOT NULL CHECK (role IN ('user','assistant')),
          content TEXT NOT NULL,
          provider TEXT NOT NULL,
          correlation_id TEXT NOT NULL,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS idx_legal_chat_conversations_user_updated
          ON legal_chat_conversations(user_id, updated_at DESC);
        CREATE INDEX IF NOT EXISTS idx_legal_chat_messages_conversation_created
          ON legal_chat_messages(conversation_id, created_at);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_legal_chat_exchange_role
          ON legal_chat_messages(user_id, correlation_id, role);
        """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<LegalChatConversation>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = """
        SELECT c.id,c.title,c.created_at,c.updated_at,COUNT(m.id)::int,
               (SELECT content FROM legal_chat_messages lm WHERE lm.conversation_id=c.id ORDER BY lm.created_at DESC LIMIT 1)
        FROM legal_chat_conversations c
        LEFT JOIN legal_chat_messages m ON m.conversation_id=c.id
        WHERE c.user_id=@userId
        GROUP BY c.id
        ORDER BY c.updated_at DESC
        LIMIT 100;
        """;
        var items = new List<LegalChatConversation>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new LegalChatConversation(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        return items;
    }

    public async Task<IReadOnlyList<LegalChatMessage>?> GetMessagesAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        const string sql = """
        SELECT m.id,m.conversation_id,m.role,m.content,m.provider,m.created_at
        FROM legal_chat_messages m
        JOIN legal_chat_conversations c ON c.id=m.conversation_id
        WHERE c.id=@conversationId AND c.user_id=@userId
        ORDER BY m.created_at;
        """;
        var items = new List<LegalChatMessage>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using (var owner = new NpgsqlCommand("SELECT 1 FROM legal_chat_conversations WHERE id=@conversationId AND user_id=@userId", connection))
        {
            owner.Parameters.AddWithValue("conversationId", conversationId);
            owner.Parameters.AddWithValue("userId", userId);
            if (await owner.ExecuteScalarAsync(ct) is null) return null;
        }
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("conversationId", conversationId);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new LegalChatMessage(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)));
        return items;
    }

    public async Task<LegalChatExchange?> SaveExchangeAsync(Guid userId, Guid? conversationId, string title, string userContent, string assistantContent, string provider, string correlationId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        Guid id;
        DateTimeOffset createdAt;
        string storedTitle;
        if (conversationId is null)
        {
            await using var create = new NpgsqlCommand("INSERT INTO legal_chat_conversations(user_id,title) VALUES(@userId,@title) RETURNING id,title,created_at", connection, transaction);
            create.Parameters.AddWithValue("userId", userId);
            create.Parameters.AddWithValue("title", title);
            await using var reader = await create.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            id = reader.GetGuid(0);
            storedTitle = reader.GetString(1);
            createdAt = reader.GetFieldValue<DateTimeOffset>(2);
        }
        else
        {
            await using var owner = new NpgsqlCommand("SELECT id,title,created_at FROM legal_chat_conversations WHERE id=@id AND user_id=@userId FOR UPDATE", connection, transaction);
            owner.Parameters.AddWithValue("id", conversationId.Value);
            owner.Parameters.AddWithValue("userId", userId);
            await using var reader = await owner.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) { await transaction.RollbackAsync(ct); return null; }
            id = reader.GetGuid(0);
            storedTitle = reader.GetString(1);
            createdAt = reader.GetFieldValue<DateTimeOffset>(2);
        }

        var userMessage = await InsertMessageAsync(connection, transaction, id, userId, "user", userContent, provider, correlationId, ct);
        var assistantMessage = await InsertMessageAsync(connection, transaction, id, userId, "assistant", assistantContent, provider, correlationId, ct);
        await using (var update = new NpgsqlCommand("UPDATE legal_chat_conversations SET updated_at=NOW() WHERE id=@id", connection, transaction))
        {
            update.Parameters.AddWithValue("id", id);
            await update.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        var conversation = new LegalChatConversation(id, storedTitle, createdAt, assistantMessage.CreatedAt, 2, assistantContent);
        return new LegalChatExchange(conversation, userMessage, assistantMessage);
    }

    private static async Task<LegalChatMessage> InsertMessageAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid conversationId, Guid userId, string role, string content, string provider, string correlationId, CancellationToken ct)
    {
        const string sql = """
        INSERT INTO legal_chat_messages(conversation_id,user_id,role,content,provider,correlation_id)
        VALUES(@conversationId,@userId,@role,@content,@provider,@correlationId)
        RETURNING id,created_at;
        """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("conversationId", conversationId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("correlationId", correlationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new LegalChatMessage(reader.GetGuid(0), conversationId, role, content, provider, reader.GetFieldValue<DateTimeOffset>(1));
    }
}
