using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresCreditPaymentStore(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS credit_payment_orders (
          id UUID PRIMARY KEY,
          user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
          plan_code TEXT NOT NULL,
          package_code TEXT NOT NULL,
          credits INTEGER NOT NULL CHECK (credits > 0),
          amount_fjd NUMERIC(12,2) NOT NULL CHECK (amount_fjd >= 0),
          currency TEXT NOT NULL DEFAULT 'FJD',
          provider TEXT NOT NULL,
          status TEXT NOT NULL DEFAULT 'pending',
          provider_session_id TEXT,
          checkout_url TEXT,
          created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
          updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
          completed_at TIMESTAMPTZ
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_credit_payment_provider_session
          ON credit_payment_orders(provider, provider_session_id)
          WHERE provider_session_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS idx_credit_payment_user_created
          ON credit_payment_orders(user_id, created_at DESC);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_credit_purchase_provider_reference
          ON credit_transactions(provider_reference)
          WHERE provider_reference IS NOT NULL AND transaction_type='purchase';
        """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<CreditPaymentOrder> CreateAsync(Guid userId, string planCode, CreditPackage package, string provider, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
        INSERT INTO credit_payment_orders(id,user_id,plan_code,package_code,credits,amount_fjd,currency,provider,status)
        VALUES(@id,@uid,@plan,@package,@credits,@amount,'FJD',@provider,'pending')
        RETURNING created_at;
        """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("uid", userId);
        command.Parameters.AddWithValue("plan", planCode);
        command.Parameters.AddWithValue("package", package.Code);
        command.Parameters.AddWithValue("credits", package.Credits);
        command.Parameters.AddWithValue("amount", package.PriceFjd);
        command.Parameters.AddWithValue("provider", provider);
        var createdAt = (DateTimeOffset)(await command.ExecuteScalarAsync(ct))!;
        return new CreditPaymentOrder(id,userId,planCode,package.Code,package.Credits,package.PriceFjd,"FJD",provider,"pending",null,null,createdAt,null);
    }

    public async Task AttachSessionAsync(Guid orderId, string sessionId, string checkoutUrl, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("UPDATE credit_payment_orders SET provider_session_id=@session,checkout_url=@url,updated_at=NOW() WHERE id=@id AND status='pending'", connection);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("url", checkoutUrl);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<CreditPaymentOrder?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        const string sql = "SELECT id,user_id,plan_code,package_code,credits,amount_fjd,currency,provider,status,provider_session_id,checkout_url,created_at,completed_at FROM credit_payment_orders WHERE id=@id";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", orderId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Read(reader);
    }

    public async Task MarkFailedAsync(Guid orderId, string status, CancellationToken ct = default)
    {
        var allowed = status is "declined" or "cancelled" or "failed" ? status : "failed";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("UPDATE credit_payment_orders SET status=@status,updated_at=NOW() WHERE id=@id AND status='pending'", connection);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("status", allowed);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> CompleteAndGrantAsync(Guid orderId, string providerReference, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string selectSql = "SELECT user_id,credits,status FROM credit_payment_orders WHERE id=@id FOR UPDATE";
        await using var select = new NpgsqlCommand(selectSql, connection, tx);
        select.Parameters.AddWithValue("id", orderId);
        await using var reader = await select.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.DisposeAsync(); await tx.RollbackAsync(ct); return false; }
        var userId = reader.GetGuid(0);
        var credits = reader.GetInt32(1);
        var status = reader.GetString(2);
        await reader.DisposeAsync();
        if (status == "completed") { await tx.CommitAsync(ct); return true; }
        if (status != "pending") { await tx.RollbackAsync(ct); return false; }

        await using (var ensure = new NpgsqlCommand("INSERT INTO credit_wallets(user_id) VALUES(@uid) ON CONFLICT(user_id) DO NOTHING", connection, tx))
        { ensure.Parameters.AddWithValue("uid", userId); await ensure.ExecuteNonQueryAsync(ct); }

        await using var walletSelect = new NpgsqlCommand("SELECT balance FROM credit_wallets WHERE user_id=@uid FOR UPDATE", connection, tx);
        walletSelect.Parameters.AddWithValue("uid", userId);
        var before = Convert.ToInt32(await walletSelect.ExecuteScalarAsync(ct));
        var after = checked(before + credits);

        await using var updateWallet = new NpgsqlCommand("UPDATE credit_wallets SET balance=@after,lifetime_purchased=lifetime_purchased+@credits,updated_at=NOW() WHERE user_id=@uid", connection, tx);
        updateWallet.Parameters.AddWithValue("after", after);
        updateWallet.Parameters.AddWithValue("credits", credits);
        updateWallet.Parameters.AddWithValue("uid", userId);
        await updateWallet.ExecuteNonQueryAsync(ct);

        await using var insertTx = new NpgsqlCommand("INSERT INTO credit_transactions(user_id,transaction_type,status,amount,balance_before,balance_after,provider_reference,metadata) VALUES(@uid,'purchase','completed',@credits,@before,@after,@provider,'{}'::jsonb) ON CONFLICT DO NOTHING", connection, tx);
        insertTx.Parameters.AddWithValue("uid", userId);
        insertTx.Parameters.AddWithValue("credits", credits);
        insertTx.Parameters.AddWithValue("before", before);
        insertTx.Parameters.AddWithValue("after", after);
        insertTx.Parameters.AddWithValue("provider", providerReference);
        var inserted = await insertTx.ExecuteNonQueryAsync(ct);
        if (inserted == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await using var orderUpdate = new NpgsqlCommand("UPDATE credit_payment_orders SET status='completed',completed_at=NOW(),updated_at=NOW() WHERE id=@id", connection, tx);
        orderUpdate.Parameters.AddWithValue("id", orderId);
        await orderUpdate.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static CreditPaymentOrder Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetString(8),
        reader.IsDBNull(9)?null:reader.GetString(9), reader.IsDBNull(10)?null:reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12)?null:reader.GetFieldValue<DateTimeOffset>(12));
}
