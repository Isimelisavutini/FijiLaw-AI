using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresMembershipSecurityStore(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS email_verification_tokens (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
              token_hash TEXT NOT NULL UNIQUE,
              expires_at TIMESTAMPTZ NOT NULL,
              consumed_at TIMESTAMPTZ,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS membership_audit_events (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID REFERENCES app_users(id) ON DELETE SET NULL,
              actor_user_id UUID REFERENCES app_users(id) ON DELETE SET NULL,
              event_type TEXT NOT NULL,
              reason TEXT,
              metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_email_verification_user ON email_verification_tokens(user_id, expires_at DESC);
            CREATE INDEX IF NOT EXISTS idx_membership_audit_user ON membership_audit_events(user_id, created_at DESC);
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);

        var concurrentIndexes = new[]
        {
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_membership_audit_created ON membership_audit_events(created_at DESC);",
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_membership_audit_type_created ON membership_audit_events(event_type, created_at DESC);"
        };
        foreach (var indexSql in concurrentIndexes)
        {
            await using var indexCommand = new NpgsqlCommand(indexSql, connection);
            await indexCommand.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<(Guid UserId, string Email, string Token)?> CreateVerificationTokenAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string findSql = "SELECT id,email,email_verified FROM app_users WHERE email=@email AND status IN ('pending','active') LIMIT 1;";
        await using var find = new NpgsqlCommand(findSql, connection, transaction);
        find.Parameters.AddWithValue("email", normalized);
        await using var reader = await find.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var userId = reader.GetGuid(0);
        var resolvedEmail = reader.GetString(1);
        var alreadyVerified = reader.GetBoolean(2);
        await reader.DisposeAsync();
        if (alreadyVerified) return null;

        const string revokeSql = "UPDATE email_verification_tokens SET consumed_at=NOW() WHERE user_id=@userId AND consumed_at IS NULL;";
        await using (var revoke = new NpgsqlCommand(revokeSql, connection, transaction))
        {
            revoke.Parameters.AddWithValue("userId", userId);
            await revoke.ExecuteNonQueryAsync(ct);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashToken(token);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        const string insertSql = "INSERT INTO email_verification_tokens (user_id,token_hash,expires_at) VALUES (@userId,@hash,@expiresAt);";
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("userId", userId);
            insert.Parameters.AddWithValue("hash", tokenHash);
            insert.Parameters.AddWithValue("expiresAt", expiresAt);
            await insert.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return (userId, resolvedEmail, token);
    }

    public async Task<Guid?> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string tokenSql = """
            SELECT user_id FROM email_verification_tokens
            WHERE token_hash=@hash AND consumed_at IS NULL AND expires_at>NOW()
            FOR UPDATE;
            """;
        await using var find = new NpgsqlCommand(tokenSql, connection, transaction);
        find.Parameters.AddWithValue("hash", HashToken(token));
        var value = await find.ExecuteScalarAsync(ct);
        if (value is not Guid userId) return null;

        const string verifySql = "UPDATE app_users SET email_verified=TRUE,updated_at=NOW() WHERE id=@userId;";
        await using (var verify = new NpgsqlCommand(verifySql, connection, transaction))
        {
            verify.Parameters.AddWithValue("userId", userId);
            await verify.ExecuteNonQueryAsync(ct);
        }

        const string consumeSql = "UPDATE email_verification_tokens SET consumed_at=NOW() WHERE token_hash=@hash;";
        await using (var consume = new NpgsqlCommand(consumeSql, connection, transaction))
        {
            consume.Parameters.AddWithValue("hash", HashToken(token));
            await consume.ExecuteNonQueryAsync(ct);
        }

        await RecordAuditAsync(connection, transaction, userId, userId, "email_verified", "Email verification completed", ct);
        await transaction.CommitAsync(ct);
        return userId;
    }

    public async Task<bool> AssignRoleAsync(Guid targetUserId, string roleCode, Guid actorUserId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return false;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string sql = """
            INSERT INTO user_roles (user_id,role_id)
            SELECT @targetUserId,id FROM roles WHERE code=@roleCode
            ON CONFLICT DO NOTHING;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetUserId", targetUserId);
        command.Parameters.AddWithValue("roleCode", roleCode.Trim().ToLowerInvariant());
        var affected = await command.ExecuteNonQueryAsync(ct);

        await RecordAuditAsync(connection, transaction, targetUserId, actorUserId, "role_assigned", reason, ct);
        await transaction.CommitAsync(ct);
        return affected > 0;
    }

    public async Task RecordAuditAsync(Guid? userId, Guid? actorUserId, string eventType, string? reason, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await RecordAuditAsync(connection, transaction, userId, actorUserId, eventType, reason, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task RecordAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid? userId, Guid? actorUserId, string eventType, string? reason, CancellationToken ct)
    {
        const string sql = "INSERT INTO membership_audit_events (user_id,actor_user_id,event_type,reason) VALUES (@userId,@actorUserId,@eventType,@reason);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("actorUserId", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
