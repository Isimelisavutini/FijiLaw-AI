using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresMembershipAuthStore(string connectionString)
{
    private const int Iterations = 210_000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);
    private static readonly Regex FijiPhonePattern = new(@"^\+679\d{7}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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

            CREATE TABLE IF NOT EXISTS password_reset_tokens (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
              token_hash TEXT NOT NULL UNIQUE,
              expires_at TIMESTAMPTZ NOT NULL,
              consumed_at TIMESTAMPTZ,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_auth_sessions_user ON auth_sessions(user_id, expires_at DESC);
            CREATE INDEX IF NOT EXISTS idx_auth_sessions_token ON auth_sessions(token_hash);
            CREATE INDEX IF NOT EXISTS idx_password_reset_user ON password_reset_tokens(user_id, expires_at DESC);
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

        await EnsureCitizenAccessAsync(connection, transaction, userId, ct);
        var session = await CreateSessionAsync(connection, transaction, userId, email, request.DisplayName?.Trim(), null, false, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    /// <summary>
    /// Creates or links a FijiLaw member after a trusted upstream identity provider has
    /// completed verification. The API endpoint that calls this method is protected by
    /// a server-to-server bridge secret; browsers must never call this method directly.
    /// </summary>
    public async Task<AuthSessionResult> CreateExternalIdentitySessionAsync(ExternalIdentitySessionRequest request, CancellationToken ct = default)
    {
        var provider = NormalizeIdentityProvider(request.IdentityProvider);
        var subject = string.IsNullOrWhiteSpace(request.IdentitySubject)
            ? throw new ArgumentException("A verified identity subject is required.")
            : request.IdentitySubject.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : NormalizeEmail(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : NormalizeFijiPhone(request.PhoneNumber);
        var requestedPlan = NormalizeRequestedPlan(request.RequestedPlanCode);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();

        if (!request.EmailVerified && !request.PhoneVerified)
            throw new ArgumentException("The upstream identity must verify an email address or Fiji mobile number.");
        if (request.EmailVerified && email is null)
            throw new ArgumentException("A verified email address is required.");
        if (request.PhoneVerified && phone is null)
            throw new ArgumentException("A verified Fiji mobile number is required.");
        if (provider == "phone" && !request.PhoneVerified)
            throw new ArgumentException("Phone registration requires a verified Fiji mobile number.");
        if (provider is "google" or "apple" && !request.EmailVerified)
            throw new ArgumentException("Google and Apple registration require a verified email address.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        Guid? userId = null;
        string? storedEmail = null;
        string? storedDisplayName = null;
        string? storedPhone = null;

        const string identitySql = """
            SELECT id,email,display_name,phone_number
            FROM app_users
            WHERE identity_provider=@provider AND identity_subject=@subject AND status='active'
            LIMIT 1
            FOR UPDATE;
            """;
        await using (var identity = new NpgsqlCommand(identitySql, connection, transaction))
        {
            identity.Parameters.AddWithValue("provider", provider);
            identity.Parameters.AddWithValue("subject", subject);
            await using var reader = await identity.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                userId = reader.GetGuid(0);
                storedEmail = reader.GetString(1);
                storedDisplayName = reader.IsDBNull(2) ? null : reader.GetString(2);
                storedPhone = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        if (userId is null && email is not null && request.EmailVerified)
        {
            const string emailSql = "SELECT id,email,display_name,phone_number FROM app_users WHERE email=@email AND status='active' LIMIT 1 FOR UPDATE;";
            await using var byEmail = new NpgsqlCommand(emailSql, connection, transaction);
            byEmail.Parameters.AddWithValue("email", email);
            await using var reader = await byEmail.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                userId = reader.GetGuid(0);
                storedEmail = reader.GetString(1);
                storedDisplayName = reader.IsDBNull(2) ? null : reader.GetString(2);
                storedPhone = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        if (userId is null && phone is not null && request.PhoneVerified)
        {
            const string phoneSql = "SELECT id,email,display_name,phone_number FROM app_users WHERE phone_number=@phone AND status='active' LIMIT 1 FOR UPDATE;";
            await using var byPhone = new NpgsqlCommand(phoneSql, connection, transaction);
            byPhone.Parameters.AddWithValue("phone", phone);
            await using var reader = await byPhone.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                userId = reader.GetGuid(0);
                storedEmail = reader.GetString(1);
                storedDisplayName = reader.IsDBNull(2) ? null : reader.GetString(2);
                storedPhone = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        if (userId is null)
        {
            userId = Guid.NewGuid();
            storedEmail = email ?? CreatePhoneIdentityAlias(phone!);
            storedDisplayName = displayName;
            storedPhone = phone;

            const string createSql = """
                INSERT INTO app_users
                  (id,email,display_name,identity_provider,identity_subject,phone_number,requested_plan_code,email_verified,phone_verified,identity_verified_at,status)
                VALUES
                  (@id,@email,@displayName,@provider,@subject,@phone,@requestedPlan,@emailVerified,@phoneVerified,NOW(),'active');
                """;
            await using var create = new NpgsqlCommand(createSql, connection, transaction);
            create.Parameters.AddWithValue("id", userId.Value);
            create.Parameters.AddWithValue("email", storedEmail);
            create.Parameters.AddWithValue("displayName", (object?)displayName ?? DBNull.Value);
            create.Parameters.AddWithValue("provider", provider);
            create.Parameters.AddWithValue("subject", subject);
            create.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
            create.Parameters.AddWithValue("requestedPlan", (object?)requestedPlan ?? DBNull.Value);
            create.Parameters.AddWithValue("emailVerified", request.EmailVerified);
            create.Parameters.AddWithValue("phoneVerified", request.PhoneVerified);
            try { await create.ExecuteNonQueryAsync(ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ArgumentException("This verified identity is already linked to another FijiLaw account.");
            }
        }
        else
        {
            var effectiveEmail = email ?? storedEmail!;
            var effectiveName = displayName ?? storedDisplayName;
            var effectivePhone = phone ?? storedPhone;
            const string updateSql = """
                UPDATE app_users
                SET email=@email,
                    display_name=COALESCE(@displayName,display_name),
                    identity_provider=@provider,
                    identity_subject=@subject,
                    phone_number=COALESCE(@phone,phone_number),
                    requested_plan_code=COALESCE(@requestedPlan,requested_plan_code),
                    email_verified=email_verified OR @emailVerified,
                    phone_verified=phone_verified OR @phoneVerified,
                    identity_verified_at=COALESCE(identity_verified_at,NOW()),
                    updated_at=NOW()
                WHERE id=@id;
                """;
            await using var update = new NpgsqlCommand(updateSql, connection, transaction);
            update.Parameters.AddWithValue("id", userId.Value);
            update.Parameters.AddWithValue("email", effectiveEmail);
            update.Parameters.AddWithValue("displayName", (object?)effectiveName ?? DBNull.Value);
            update.Parameters.AddWithValue("provider", provider);
            update.Parameters.AddWithValue("subject", subject);
            update.Parameters.AddWithValue("phone", (object?)effectivePhone ?? DBNull.Value);
            update.Parameters.AddWithValue("requestedPlan", (object?)requestedPlan ?? DBNull.Value);
            update.Parameters.AddWithValue("emailVerified", request.EmailVerified);
            update.Parameters.AddWithValue("phoneVerified", request.PhoneVerified);
            try { await update.ExecuteNonQueryAsync(ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ArgumentException("This verified identity is already linked to another FijiLaw account.");
            }
            storedEmail = effectiveEmail;
            storedDisplayName = effectiveName;
            storedPhone = effectivePhone;
        }

        await EnsureCitizenAccessAsync(connection, transaction, userId.Value, ct);
        var session = await CreateSessionAsync(connection, transaction, userId.Value, storedEmail!, storedDisplayName, storedPhone, true, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<AuthSessionResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT u.id,u.display_name,c.password_salt,c.password_hash,c.iterations,u.phone_number,
                   (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL) AS identity_verified
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
        var phone = reader.IsDBNull(5) ? null : reader.GetString(5);
        var identityVerified = reader.GetBoolean(6);
        var actualHash = HashPassword(request.Password, salt, iterations);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new UnauthorizedAccessException("Invalid email or password.");
        await reader.DisposeAsync();

        await using var transaction = await connection.BeginTransactionAsync(ct);
        var session = await CreateSessionAsync(connection, transaction, userId, email, displayName, phone, identityVerified, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<(Guid UserId, string Email, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return null;
        var normalized = email.Trim().ToLowerInvariant();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string findSql = "SELECT id,email FROM app_users WHERE email=@email AND status='active' LIMIT 1;";
        await using var find = new NpgsqlCommand(findSql, connection, transaction);
        find.Parameters.AddWithValue("email", normalized);
        await using var reader = await find.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var userId = reader.GetGuid(0);
        var resolvedEmail = reader.GetString(1);
        await reader.DisposeAsync();

        const string revokeSql = "UPDATE password_reset_tokens SET consumed_at=NOW() WHERE user_id=@userId AND consumed_at IS NULL;";
        await using (var revoke = new NpgsqlCommand(revokeSql, connection, transaction))
        {
            revoke.Parameters.AddWithValue("userId", userId);
            await revoke.ExecuteNonQueryAsync(ct);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        const string insertSql = "INSERT INTO password_reset_tokens (user_id,token_hash,expires_at) VALUES (@userId,@hash,@expiresAt);";
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("userId", userId);
            insert.Parameters.AddWithValue("hash", HashToken(token));
            insert.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.Add(PasswordResetLifetime));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return (userId, resolvedEmail, token);
    }

    public async Task<Guid?> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        ValidatePassword(newPassword);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string tokenSql = """
            SELECT user_id FROM password_reset_tokens
            WHERE token_hash=@hash AND consumed_at IS NULL AND expires_at>NOW()
            FOR UPDATE;
            """;
        await using var find = new NpgsqlCommand(tokenSql, connection, transaction);
        find.Parameters.AddWithValue("hash", HashToken(token));
        var value = await find.ExecuteScalarAsync(ct);
        if (value is not Guid userId) return null;

        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = HashPassword(newPassword, salt, Iterations);
        const string updateSql = """
            UPDATE user_credentials
            SET password_salt=@salt,password_hash=@passwordHash,iterations=@iterations,updated_at=NOW()
            WHERE user_id=@userId;
            """;
        await using (var update = new NpgsqlCommand(updateSql, connection, transaction))
        {
            update.Parameters.AddWithValue("salt", salt);
            update.Parameters.AddWithValue("passwordHash", hash);
            update.Parameters.AddWithValue("iterations", Iterations);
            update.Parameters.AddWithValue("userId", userId);
            if (await update.ExecuteNonQueryAsync(ct) != 1) return null;
        }

        const string consumeSql = "UPDATE password_reset_tokens SET consumed_at=NOW() WHERE user_id=@userId AND consumed_at IS NULL;";
        await using (var consume = new NpgsqlCommand(consumeSql, connection, transaction))
        {
            consume.Parameters.AddWithValue("userId", userId);
            await consume.ExecuteNonQueryAsync(ct);
        }

        const string revokeSessionsSql = "UPDATE auth_sessions SET revoked_at=NOW() WHERE user_id=@userId AND revoked_at IS NULL;";
        await using (var revoke = new NpgsqlCommand(revokeSessionsSql, connection, transaction))
        {
            revoke.Parameters.AddWithValue("userId", userId);
            await revoke.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return userId;
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

    private static async Task EnsureCitizenAccessAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, CancellationToken ct)
    {
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
            SELECT @userId,id,'active','free',NOW()
            FROM subscription_plans
            WHERE code='free'
              AND NOT EXISTS (SELECT 1 FROM subscriptions WHERE user_id=@userId);
            """;
        await using var subscription = new NpgsqlCommand(freeSubscriptionSql, connection, transaction);
        subscription.Parameters.AddWithValue("userId", userId);
        await subscription.ExecuteNonQueryAsync(ct);
    }

    private static async Task<AuthSessionResult> CreateSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        string email,
        string? displayName,
        string? phoneNumber,
        bool identityVerified,
        CancellationToken ct)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
        const string sql = "INSERT INTO auth_sessions (user_id,token_hash,expires_at) VALUES (@userId,@hash,@expiresAt);";
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", HashToken(rawToken));
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return new AuthSessionResult(rawToken, expiresAt, userId, email, displayName, phoneNumber, identityVerified);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("A valid email is required.");
        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeFijiPhone(string phone)
    {
        var normalized = phone.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);
        if (normalized.StartsWith("679", StringComparison.Ordinal)) normalized = "+" + normalized;
        if (Regex.IsMatch(normalized, @"^\d{7}$", RegexOptions.CultureInvariant)) normalized = "+679" + normalized;
        if (!FijiPhonePattern.IsMatch(normalized))
            throw new ArgumentException("A Fiji mobile number must use +679 followed by the seven-digit national number.");
        return normalized;
    }

    private static string NormalizeIdentityProvider(string provider)
    {
        var value = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        return value switch
        {
            "google" or "oauth_google" or "clerk_google" => "google",
            "apple" or "oauth_apple" or "clerk_apple" => "apple",
            "phone" or "clerk_phone" or "phone_otp" => "phone",
            "email" or "clerk_email" or "email_otp" => "email_otp",
            _ => throw new ArgumentException("Unsupported identity provider.")
        };
    }

    private static string CreatePhoneIdentityAlias(string phone)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(phone))).ToLowerInvariant();
        return $"phone-{digest[..24]}@identity.fijilaw.local";
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
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
