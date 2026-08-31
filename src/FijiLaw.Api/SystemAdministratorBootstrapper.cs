using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FijiLaw.Api;

public sealed record SystemAdministratorBootstrapOptions(bool Enabled, string? Email, string? DisplayName, string? Password)
{
    public static SystemAdministratorBootstrapOptions FromConfiguration(IConfiguration configuration) => new(
        string.Equals(configuration["SYSTEM_ADMINISTRATOR_BOOTSTRAP_ENABLED"], "true", StringComparison.OrdinalIgnoreCase),
        configuration["SYSTEM_ADMINISTRATOR_BOOTSTRAP_EMAIL"]?.Trim().ToLowerInvariant(),
        configuration["SYSTEM_ADMINISTRATOR_BOOTSTRAP_DISPLAY_NAME"]?.Trim(),
        configuration["SYSTEM_ADMINISTRATOR_BOOTSTRAP_PASSWORD"]);
}

public static class SystemAdministratorBootstrapper
{
    private const int PasswordIterations = 210_000;
    private const string CompletedEvent = "system_administrator_bootstrap_completed";

    public static async Task RunAsync(string connectionString, SystemAdministratorBootstrapOptions options, bool demoAccountsSeeded, CancellationToken ct = default)
    {
        if (!options.Enabled) return;
        if (demoAccountsSeeded)
            throw new InvalidOperationException("System administrator bootstrap requires SEED_DEMO_ACCOUNTS=false.");
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException("System administrator bootstrap requires email and password configuration.");
        if (options.Password.Length < 10)
            throw new InvalidOperationException("System administrator bootstrap password must be at least 10 characters.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('fijilaw_system_administrator_bootstrap'));", connection, transaction))
            await lockCommand.ExecuteNonQueryAsync(ct);

        Guid? completedUserId;
        await using (var completed = new NpgsqlCommand("SELECT user_id FROM membership_audit_events WHERE event_type=@event LIMIT 1 FOR UPDATE;", connection, transaction))
        {
            completed.Parameters.AddWithValue("event", CompletedEvent);
            completedUserId = await completed.ExecuteScalarAsync(ct) as Guid?;
        }
        if (completedUserId is not null)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        Guid userId;
        await using (var find = new NpgsqlCommand("SELECT id FROM app_users WHERE email=@email FOR UPDATE;", connection, transaction))
        {
            find.Parameters.AddWithValue("email", options.Email);
            var existing = await find.ExecuteScalarAsync(ct);
            userId = existing is Guid id ? id : Guid.NewGuid();
        }

        if (userId == Guid.Empty) throw new InvalidOperationException("System administrator bootstrap could not resolve the account.");

        await using (var upsertUser = new NpgsqlCommand("""
            INSERT INTO app_users (id,email,display_name,requested_plan_code,email_verified,identity_verified_at,status)
            VALUES (@id,@email,@displayName,'free',TRUE,NOW(),'active')
            ON CONFLICT (email) DO UPDATE SET
              display_name=COALESCE(EXCLUDED.display_name,app_users.display_name),
              email_verified=TRUE,
              identity_verified_at=COALESCE(app_users.identity_verified_at,NOW()),
              status='active',
              updated_at=NOW();
            """, connection, transaction))
        {
            upsertUser.Parameters.AddWithValue("id", userId);
            upsertUser.Parameters.AddWithValue("email", options.Email);
            upsertUser.Parameters.AddWithValue("displayName", (object?)options.DisplayName ?? DBNull.Value);
            await upsertUser.ExecuteNonQueryAsync(ct);
        }

        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(options.Password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        await using (var credential = new NpgsqlCommand("""
            INSERT INTO user_credentials (user_id,password_salt,password_hash,iterations)
            VALUES (@id,@salt,@hash,@iterations)
            ON CONFLICT (user_id) DO UPDATE SET
              password_salt=EXCLUDED.password_salt,
              password_hash=EXCLUDED.password_hash,
              iterations=EXCLUDED.iterations,
              updated_at=NOW();
            """, connection, transaction))
        {
            credential.Parameters.AddWithValue("id", userId);
            credential.Parameters.AddWithValue("salt", salt);
            credential.Parameters.AddWithValue("hash", hash);
            credential.Parameters.AddWithValue("iterations", PasswordIterations);
            await credential.ExecuteNonQueryAsync(ct);
        }

        await using (var role = new NpgsqlCommand("""
            INSERT INTO user_roles (user_id,role_id)
            SELECT @userId,id FROM roles WHERE code='platform_admin'
            ON CONFLICT DO NOTHING;
            """, connection, transaction))
        {
            role.Parameters.AddWithValue("userId", userId);
            if (await role.ExecuteNonQueryAsync(ct) == 0)
            {
                await using var exists = new NpgsqlCommand("SELECT 1 FROM roles WHERE code='platform_admin';", connection, transaction);
                if (await exists.ExecuteScalarAsync(ct) is null)
                    throw new InvalidOperationException("platform_admin role is missing.");
            }
        }

        await using (var audit = new NpgsqlCommand("""
            INSERT INTO membership_audit_events (user_id,actor_user_id,event_type,reason,metadata)
            VALUES (@userId,@userId,@event,'One-time deployment-operator system administrator bootstrap completed.',
                    jsonb_build_object('source','railway_environment'));
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue("userId", userId);
            audit.Parameters.AddWithValue("event", CompletedEvent);
            await audit.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
