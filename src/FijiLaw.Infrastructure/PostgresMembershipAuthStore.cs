using System.Security.Cryptography;
using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresMembershipAuthStore(string connectionString)
{
    private const int Iterations = 210_000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly HashSet<string> AllowedRequestedPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        MembershipPlans.Free,
        MembershipPlans.PersonalPlus,
        MembershipPlans.LawyerProfessional,
        MembershipPlans.FirmStarter,
        MembershipPlans.FirmProfessional,
        MembershipPlans.FirmPremium,
        MembershipPlans.Institutional
    };

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS user_credentials (
              user_id UUID PRIMARY KEY REFERENCES app_users(id) ON DELETE CASCADE,
              password_salt BYTEA NOT NULL,
              password_hash BYTEA NOT NULL,
              iterations INTEGER NOT NULL,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS auth_sessions (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
              token_hash TEXT NOT NULL UNIQUE,
              expires_at TIMESTAMPTZ NOT NULL,
              revoked_at TIMESTAMPTZ,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_auth_sessions_user ON auth_sessions(user_id, expires_at DESC);
            CREATE INDEX IF NOT EXISTS idx_auth_sessions_token ON auth_sessions(token_hash);
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<AuthSessionResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);
        var requestedPlan = NormalizeRequestedPlan(request.RequestedPlanCode);
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = HashPassword(request.Password, salt, Iterations);
        var userId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string userSql = """
            INSERT INTO app_users (id,email,display_name,requested_plan_code,email_verified,status)
            VALUES (@id,@email,@displayName,@requestedPlan,FALSE,'active');
            """;
        await using (var cmd = new NpgsqlCommand(userSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("displayName", (object?)request.DisplayName?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("requestedPlan", (object?)requestedPlan ?? DBNull.Value);
            try { await cmd.ExecuteNonQueryAsync(ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ArgumentException("An account with this email already exists.");
            }
        }

        const string credentialSql = """
            INSERT INTO user_credentials (user_id,password_salt,password_hash,iterations)
            VALUES (@userId,@salt,@hash,@iterations);
            """;
        await using (var cmd = new NpgsqlCommand(credentialSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("salt", salt);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("iterations", Iterations);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string roleSql = """
            INSERT INTO user_roles (user_id, role_id)
            SELECT @userId, id FROM roles WHERE code='citizen'
            ON CONFLICT DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(roleSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("userId", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string freeSubscriptionSql = """
            INSERT INTO subscriptions (user_id,plan_id,status,billing_interval,started_at)
            SELECT @userId,id,'active','free',NOW() FROM subscription_plans WHERE code='free';
            """;
        await using (var cmd = new NpgsqlCommand(freeSubscriptionSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("userId", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var session = await CreateSessionAsync(connection, transaction, userId, email, request.DisplayName?.Trim(), ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<AuthSessionResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT u.id,u.display_name,c.password_salt,c.password_hash,c.iterations
            FROM app_users u
            JOIN user_credentials c ON c.user_id=u.id
            WHERE u.email=@email AND u.status='active';
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("email", email);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new UnauthorizedAccessException("Invalid email or password.");

        var userId = reader.GetGuid(0);
        var displayName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var salt = (byte[])reader[2];
        var expectedHash = (byte[])reader[3];
        var iterations = reader.GetInt32(4);
        var actualHash = HashPassword(request.Password, salt, iterations);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new UnauthorizedAccessException("Invalid email or password.");
        await reader.DisposeAsync();

        await using var transaction = await connection.BeginTransactionAsync(ct);
        var session = await CreateSessionAsync(connection, transaction, userId, email, displayName, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<Guid?> ValidateSessionAsync(string? bearerToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken)) return null;
        var tokenHash = HashToken(bearerToken);
        const string sql = """
            SELECT user_id FROM auth_sessions
            WHERE token_hash=@hash AND revoked_at IS NULL AND expires_at>NOW()
            LIMIT 1;
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("hash", tokenHash);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is Guid id ? id : null;
    }

    public async Task RevokeSessionAsync(string? bearerToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken)) return;
        const string sql = "UPDATE auth_sessions SET revoked_at=NOW() WHERE token_hash=@hash AND revoked_at IS NULL;";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("hash", HashToken(bearerToken));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<AuthSessionResult> CreateSessionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, string email, string? displayName, CancellationToken ct)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
        const string sql = "INSERT INTO auth_sessions (user_id,token_hash,expires_at) VALUES (@userId,@hash,@expiresAt);";
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", HashToken(rawToken));
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AuthSessionResult(rawToken, expiresAt, userId, email, displayName);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("A valid email is required.");
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeRequestedPlan(string? requestedPlanCode)
    {
        if (string.IsNullOrWhiteSpace(requestedPlanCode)) return MembershipPlans.Free;
        var normalized = requestedPlanCode.Trim().ToLowerInvariant();
        if (!AllowedRequestedPlans.Contains(normalized)) throw new ArgumentException("The selected membership plan is not valid.");
        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            throw new ArgumentException("Password must be at least 10 characters long.");
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
