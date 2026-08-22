using FijiLaw.AI;
using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresLegalSourceRetriever(string connectionString) : ILegalSourceRetriever
{
    public async Task<IReadOnlyList<LegalAuthority>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<LegalAuthority>();

        const string sql = """
            SELECT title, provision, canonical_url, verified
            FROM legal_sources
            WHERE verified = TRUE
              AND (
                    title ILIKE '%' || @q || '%'
                 OR COALESCE(provision, '') ILIKE '%' || @q || '%'
                 OR content ILIKE '%' || @q || '%'
              )
            ORDER BY
              CASE WHEN title ILIKE '%' || @q || '%' THEN 0 ELSE 1 END,
              created_at DESC
            LIMIT 8;
            """;

        var results = new List<LegalAuthority>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("q", query.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new LegalAuthority(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetBoolean(3)));
        }

        return results;
    }
}

public sealed class DatabaseInitializer(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS legal_sources (
              id UUID PRIMARY KEY,
              jurisdiction TEXT NOT NULL DEFAULT 'FJ',
              source_type TEXT NOT NULL,
              title TEXT NOT NULL,
              provision TEXT,
              canonical_url TEXT,
              effective_date DATE,
              verified BOOLEAN NOT NULL DEFAULT FALSE,
              content TEXT NOT NULL,
              content_hash TEXT NOT NULL,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS ai_audit_events (
              id UUID PRIMARY KEY,
              correlation_id TEXT NOT NULL,
              event_type TEXT NOT NULL,
              payload JSONB NOT NULL,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_legal_sources_verified ON legal_sources(verified);
            CREATE INDEX IF NOT EXISTS idx_audit_correlation ON ai_audit_events(correlation_id);
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
