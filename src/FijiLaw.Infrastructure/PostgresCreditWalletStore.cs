using System.Text.Json;
using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresCreditWalletStore(string connectionString) : ICreditWalletStore
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS credit_wallets (
          user_id UUID PRIMARY KEY REFERENCES app_users(id) ON DELETE CASCADE,
          balance INTEGER NOT NULL DEFAULT 0 CHECK (balance >= 0),
          lifetime_purchased BIGINT NOT NULL DEFAULT 0,
          lifetime_granted BIGINT NOT NULL DEFAULT 0,
          lifetime_used BIGINT NOT NULL DEFAULT 0,
          last_allowance_key TEXT,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
          updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS credit_transactions (
          id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
          user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
          transaction_type TEXT NOT NULL,
          status TEXT NOT NULL DEFAULT 'completed',
          amount INTEGER NOT NULL,
          balance_before INTEGER NOT NULL,
          balance_after INTEGER NOT NULL,
          service_code TEXT,
          correlation_id TEXT,
          provider_reference TEXT,
          metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
          updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_credit_tx_correlation_usage
          ON credit_transactions(user_id, correlation_id, service_code)
          WHERE correlation_id IS NOT NULL AND transaction_type='usage';
        CREATE INDEX IF NOT EXISTS idx_credit_tx_user_created ON credit_transactions(user_id, created_at DESC);
        """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<CreditWalletSnapshot> GetWalletAsync(Guid userId, string planCode, CancellationToken ct = default)
    {
        await EnsureAllowanceAsync(userId, planCode, ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT balance,lifetime_purchased,lifetime_granted,lifetime_used,last_allowance_key FROM credit_wallets WHERE user_id=@uid", connection);
        command.Parameters.AddWithValue("uid", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Credit wallet could not be loaded.");
        return new CreditWalletSnapshot(userId, reader.GetInt32(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    public async Task<CreditReservation?> ReserveAsync(Guid userId, string planCode, int credits, string serviceCode, string correlationId, CancellationToken ct = default)
    {
        if (credits <= 0) throw new ArgumentOutOfRangeException(nameof(credits));
        await EnsureAllowanceAsync(userId, planCode, ct);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using var select = new NpgsqlCommand("SELECT balance FROM credit_wallets WHERE user_id=@uid FOR UPDATE", connection, tx);
        select.Parameters.AddWithValue("uid", userId);
        var balanceObj = await select.ExecuteScalarAsync(ct);
        if (balanceObj is null) return null;
        var before = Convert.ToInt32(balanceObj);
        if (before < credits) { await tx.RollbackAsync(ct); return null; }
        var after = before - credits;

        await using var update = new NpgsqlCommand("UPDATE credit_wallets SET balance=@after,updated_at=NOW() WHERE user_id=@uid", connection, tx);
        update.Parameters.AddWithValue("after", after); update.Parameters.AddWithValue("uid", userId);
        await update.ExecuteNonQueryAsync(ct);

        var id = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("INSERT INTO credit_transactions(id,user_id,transaction_type,status,amount,balance_before,balance_after,service_code,correlation_id) VALUES(@id,@uid,'usage','reserved',@amount,@before,@after,@service,@correlation)", connection, tx);
        insert.Parameters.AddWithValue("id", id); insert.Parameters.AddWithValue("uid", userId); insert.Parameters.AddWithValue("amount", -credits); insert.Parameters.AddWithValue("before", before); insert.Parameters.AddWithValue("after", after); insert.Parameters.AddWithValue("service", serviceCode); insert.Parameters.AddWithValue("correlation", correlationId);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new CreditReservation(id, userId, credits, serviceCode, correlationId);
    }

    public async Task CompleteAsync(CreditReservation reservation, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var updateTx = new NpgsqlCommand("UPDATE credit_transactions SET status='completed',updated_at=NOW() WHERE id=@id AND status='reserved'", connection, tx);
        updateTx.Parameters.AddWithValue("id", reservation.TransactionId);
        var changed = await updateTx.ExecuteNonQueryAsync(ct);
        if (changed > 0)
        {
            await using var updateWallet = new NpgsqlCommand("UPDATE credit_wallets SET lifetime_used=lifetime_used+@credits,updated_at=NOW() WHERE user_id=@uid", connection, tx);
            updateWallet.Parameters.AddWithValue("credits", reservation.Credits); updateWallet.Parameters.AddWithValue("uid", reservation.UserId);
            await updateWallet.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task RefundAsync(CreditReservation reservation, string reason, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var select = new NpgsqlCommand("SELECT status,balance_after FROM credit_transactions WHERE id=@id FOR UPDATE", connection, tx);
        select.Parameters.AddWithValue("id", reservation.TransactionId);
        await using var reader = await select.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.GetString(0) != "reserved") { await reader.DisposeAsync(); await tx.RollbackAsync(ct); return; }
        await reader.DisposeAsync();

        await using var wallet = new NpgsqlCommand("SELECT balance FROM credit_wallets WHERE user_id=@uid FOR UPDATE", connection, tx);
        wallet.Parameters.AddWithValue("uid", reservation.UserId);
        var before = Convert.ToInt32(await wallet.ExecuteScalarAsync(ct));
        var after = before + reservation.Credits;
        await using var updateWallet = new NpgsqlCommand("UPDATE credit_wallets SET balance=@after,updated_at=NOW() WHERE user_id=@uid", connection, tx);
        updateWallet.Parameters.AddWithValue("after", after); updateWallet.Parameters.AddWithValue("uid", reservation.UserId);
        await updateWallet.ExecuteNonQueryAsync(ct);

        await using var updateTx = new NpgsqlCommand("UPDATE credit_transactions SET status='refunded',metadata=@metadata::jsonb,updated_at=NOW() WHERE id=@id", connection, tx);
        updateTx.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(new { reason })); updateTx.Parameters.AddWithValue("id", reservation.TransactionId);
        await updateTx.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<CreditWalletSnapshot> GrantAsync(Guid userId, string planCode, int credits, string reason, bool purchased = false, string? providerReference = null, CancellationToken ct = default)
    {
        if (credits <= 0) throw new ArgumentOutOfRangeException(nameof(credits));
        await EnsureAllowanceAsync(userId, planCode, ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var select = new NpgsqlCommand("SELECT balance FROM credit_wallets WHERE user_id=@uid FOR UPDATE", connection, tx);
        select.Parameters.AddWithValue("uid", userId);
        var before = Convert.ToInt32(await select.ExecuteScalarAsync(ct));
        var after = before + credits;
        var lifetimeColumn = purchased ? "lifetime_purchased" : "lifetime_granted";
        await using var update = new NpgsqlCommand($"UPDATE credit_wallets SET balance=@after,{lifetimeColumn}={lifetimeColumn}+@credits,updated_at=NOW() WHERE user_id=@uid", connection, tx);
        update.Parameters.AddWithValue("after", after); update.Parameters.AddWithValue("credits", credits); update.Parameters.AddWithValue("uid", userId);
        await update.ExecuteNonQueryAsync(ct);
        await using var insert = new NpgsqlCommand("INSERT INTO credit_transactions(user_id,transaction_type,status,amount,balance_before,balance_after,provider_reference,metadata) VALUES(@uid,@type,'completed',@amount,@before,@after,@provider,@metadata::jsonb)", connection, tx);
        insert.Parameters.AddWithValue("uid", userId); insert.Parameters.AddWithValue("type", purchased ? "purchase" : "adjustment"); insert.Parameters.AddWithValue("amount", credits); insert.Parameters.AddWithValue("before", before); insert.Parameters.AddWithValue("after", after); insert.Parameters.AddWithValue("provider", (object?)providerReference ?? DBNull.Value); insert.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(new { reason }));
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return await GetWalletAsync(userId, planCode, ct);
    }

    public async Task<IReadOnlyList<CreditTransactionSummary>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var items = new List<CreditTransactionSummary>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT id,transaction_type,status,amount,balance_before,balance_after,service_code,correlation_id,provider_reference,created_at FROM credit_transactions WHERE user_id=@uid ORDER BY created_at DESC LIMIT @limit", connection);
        command.Parameters.AddWithValue("uid", userId); command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new CreditTransactionSummary(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)));
        return items;
    }

    private async Task EnsureAllowanceAsync(Guid userId, string planCode, CancellationToken ct)
    {
        var allowance = FijiLawCreditCatalog.IncludedCredits(planCode);
        var key = FijiLawCreditCatalog.AllowanceKey(planCode, DateTimeOffset.UtcNow);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var ensure = new NpgsqlCommand("INSERT INTO credit_wallets(user_id) VALUES(@uid) ON CONFLICT(user_id) DO NOTHING", connection, tx))
        { ensure.Parameters.AddWithValue("uid", userId); await ensure.ExecuteNonQueryAsync(ct); }
        await using var select = new NpgsqlCommand("SELECT balance,last_allowance_key FROM credit_wallets WHERE user_id=@uid FOR UPDATE", connection, tx);
        select.Parameters.AddWithValue("uid", userId);
        await using var reader = await select.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var before = reader.GetInt32(0); var lastKey = reader.IsDBNull(1) ? null : reader.GetString(1);
        await reader.DisposeAsync();
        if (allowance > 0 && !string.Equals(lastKey, key, StringComparison.Ordinal))
        {
            var after = before + allowance;
            await using var update = new NpgsqlCommand("UPDATE credit_wallets SET balance=@after,lifetime_granted=lifetime_granted+@allowance,last_allowance_key=@key,updated_at=NOW() WHERE user_id=@uid", connection, tx);
            update.Parameters.AddWithValue("after", after); update.Parameters.AddWithValue("allowance", allowance); update.Parameters.AddWithValue("key", key); update.Parameters.AddWithValue("uid", userId);
            await update.ExecuteNonQueryAsync(ct);
            await using var insert = new NpgsqlCommand("INSERT INTO credit_transactions(user_id,transaction_type,status,amount,balance_before,balance_after,metadata) VALUES(@uid,'allowance','completed',@amount,@before,@after,@metadata::jsonb)", connection, tx);
            insert.Parameters.AddWithValue("uid", userId); insert.Parameters.AddWithValue("amount", allowance); insert.Parameters.AddWithValue("before", before); insert.Parameters.AddWithValue("after", after); insert.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(new { planCode, allowanceKey = key }));
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
