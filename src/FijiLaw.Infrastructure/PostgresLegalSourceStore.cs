using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed record LegalSourceInput(
    string SourceType,
    string Title,
    string? Provision,
    string? CanonicalUrl,
    DateOnly? EffectiveDate,
    string Content,
    bool Verified = false);

public sealed class PostgresLegalSourceStore(string connectionString)
{
    public async Task<Guid> UpsertAsync(LegalSourceInput input, string correlationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.SourceType)) throw new ArgumentException("SourceType is required.");
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(input.Content)) throw new ArgumentException("Content is required.");

        var id = Guid.NewGuid();
        var content = input.Content.Trim();
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        const string sql = """
            INSERT INTO legal_sources
                (id, jurisdiction, source_type, title, provision, canonical_url, effective_date, verified, content, content_hash)
            VALUES
                (@id, 'FJ', @source_type, @title, @provision, @canonical_url, @effective_date, @verified, @content, @content_hash)
            ON CONFLICT (content_hash) DO UPDATE SET
                source_type = EXCLUDED.source_type,
                title = EXCLUDED.title,
                provision = EXCLUDED.provision,
                canonical_url = EXCLUDED.canonical_url,
                effective_date = EXCLUDED.effective_date,
                verified = EXCLUDED.verified,
                content = EXCLUDED.content
            RETURNING id;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("source_type", input.SourceType.Trim());
        command.Parameters.AddWithValue("title", input.Title.Trim());
        command.Parameters.AddWithValue("provision", (object?)input.Provision?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("canonical_url", (object?)input.CanonicalUrl?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("effective_date", (object?)input.EffectiveDate ?? DBNull.Value);
        command.Parameters.AddWithValue("verified", input.Verified);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("content_hash", contentHash);

        var returned = (Guid)(await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Legal source insert failed."));

        const string auditSql = """
            INSERT INTO ai_audit_events (id, correlation_id, event_type, payload)
            VALUES (@id, @correlation_id, 'legal_source_upsert', jsonb_build_object(
                'legal_source_id', @legal_source_id,
                'title', @title,
                'verified', @verified,
                'content_hash', @content_hash
            ));
            """;
        await using var audit = new NpgsqlCommand(auditSql, connection, transaction);
        audit.Parameters.AddWithValue("id", Guid.NewGuid());
        audit.Parameters.AddWithValue("correlation_id", correlationId);
        audit.Parameters.AddWithValue("legal_source_id", returned);
        audit.Parameters.AddWithValue("title", input.Title.Trim());
        audit.Parameters.AddWithValue("verified", input.Verified);
        audit.Parameters.AddWithValue("content_hash", contentHash);
        await audit.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        return returned;
    }
}
