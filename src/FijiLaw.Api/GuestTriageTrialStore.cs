using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FijiLaw.Api;

public sealed record GuestTriageTrialStatus(int Used, int Remaining, int Limit, bool Exhausted);

public sealed class GuestTriageTrialStore(string? databaseUrl)
{
    public const int TrialLimit = 3;
    private readonly ConcurrentDictionary<string, int> _memory = new(StringComparer.Ordinal);

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl)) return;
        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            CREATE TABLE IF NOT EXISTS guest_triage_trials (
              guest_hash TEXT PRIMARY KEY,
              successful_triages INTEGER NOT NULL DEFAULT 0,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              CHECK (successful_triages >= 0)
            );
            CREATE INDEX IF NOT EXISTS idx_guest_triage_trials_updated
              ON guest_triage_trials(updated_at DESC);
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<GuestTriageTrialStatus> GetStatusAsync(string guestId, CancellationToken ct = default)
    {
        var key = Hash(ValidateGuestId(guestId));
        var used = string.IsNullOrWhiteSpace(databaseUrl)
            ? _memory.GetValueOrDefault(key, 0)
            : await GetDatabaseCountAsync(key, ct);
        return Status(used);
    }

    public async Task<GuestTriageTrialStatus?> TryReserveAsync(string guestId, CancellationToken ct = default)
    {
        var key = Hash(ValidateGuestId(guestId));
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            while (true)
            {
                var current = _memory.GetValueOrDefault(key, 0);
                if (current >= TrialLimit) return null;
                if (_memory.TryUpdate(key, current + 1, current) || (current == 0 && _memory.TryAdd(key, 1)))
                    return Status(current + 1);
            }
        }

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insert = """
            INSERT INTO guest_triage_trials (guest_hash, successful_triages)
            VALUES (@guest_hash, 0)
            ON CONFLICT (guest_hash) DO NOTHING;
            """;
        await using (var insertCommand = new NpgsqlCommand(insert, connection, transaction))
        {
            insertCommand.Parameters.AddWithValue("guest_hash", key);
            await insertCommand.ExecuteNonQueryAsync(ct);
        }

        const string update = """
            UPDATE guest_triage_trials
            SET successful_triages = successful_triages + 1,
                updated_at = NOW()
            WHERE guest_hash = @guest_hash
              AND successful_triages < @limit
            RETURNING successful_triages;
            """;
        await using var updateCommand = new NpgsqlCommand(update, connection, transaction);
        updateCommand.Parameters.AddWithValue("guest_hash", key);
        updateCommand.Parameters.AddWithValue("limit", TrialLimit);
        var value = await updateCommand.ExecuteScalarAsync(ct);
        if (value is null)
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        await transaction.CommitAsync(ct);
        return Status(Convert.ToInt32(value));
    }

    public async Task ReleaseAsync(string guestId, CancellationToken ct = default)
    {
        var key = Hash(ValidateGuestId(guestId));
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            while (true)
            {
                var current = _memory.GetValueOrDefault(key, 0);
                if (current <= 0) return;
                if (_memory.TryUpdate(key, current - 1, current)) return;
            }
        }

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            UPDATE guest_triage_trials
            SET successful_triages = GREATEST(successful_triages - 1, 0),
                updated_at = NOW()
            WHERE guest_hash = @guest_hash;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("guest_hash", key);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> GetDatabaseCountAsync(string key, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = "SELECT successful_triages FROM guest_triage_trials WHERE guest_hash=@guest_hash";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("guest_hash", key);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    private static GuestTriageTrialStatus Status(int used)
    {
        used = Math.Clamp(used, 0, TrialLimit);
        return new GuestTriageTrialStatus(used, Math.Max(0, TrialLimit - used), TrialLimit, used >= TrialLimit);
    }

    private static string ValidateGuestId(string guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId) || guestId.Length is < 16 or > 128)
            throw new ArgumentException("A valid FijiLaw guest trial identifier is required.");
        return guestId.Trim();
    }

    private static string Hash(string guestId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"fijilaw-guest-trial:v1:{guestId}"))).ToLowerInvariant();
}
