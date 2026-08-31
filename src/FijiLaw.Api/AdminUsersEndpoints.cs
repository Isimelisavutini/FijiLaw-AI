using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public static class AdminUsersEndpoints
{
    private const long AdministratorMutationLock = 726_452_101;

    public static WebApplication MapAdminUsersEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapGet("/api/admin/users", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;

            var page = ParseBoundedInt(request.Query["page"], 1, 1, int.MaxValue);
            var pageSize = ParseBoundedInt(request.Query["pageSize"], 50, 1, 100);
            var query = request.Query["q"].ToString().Trim();
            var offset = (page - 1) * pageSize;

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);

            const string countSql = """
                SELECT COUNT(*)
                FROM app_users u
                WHERE @query='' OR u.email ILIKE '%' || @query || '%' OR COALESCE(u.display_name,'') ILIKE '%' || @query || '%';
                """;
            int total;
            await using (var count = new NpgsqlCommand(countSql, connection))
            {
                count.Parameters.AddWithValue("query", query);
                total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
            }

            const string usersSql = """
                SELECT u.id,u.email,u.display_name,u.email_verified,
                       (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL) AS identity_verified,
                       u.status,
                       COALESCE((SELECT array_agg(r.code ORDER BY r.code)
                                 FROM user_roles ur JOIN roles r ON r.id=ur.role_id
                                 WHERE ur.user_id=u.id),'{}'::text[]) AS roles,
                       COALESCE(sub.plan_code,u.requested_plan_code,'free') AS plan_code,
                       COALESCE(sub.subscription_status,'inactive') AS subscription_status,
                       (SELECT COUNT(*) FROM auth_sessions s
                        WHERE s.user_id=u.id AND s.revoked_at IS NULL AND s.expires_at>NOW()) AS active_sessions,
                       u.created_at,u.updated_at
                FROM app_users u
                LEFT JOIN LATERAL (
                    SELECT sp.code AS plan_code,s.status AS subscription_status
                    FROM subscriptions s
                    JOIN subscription_plans sp ON sp.id=s.plan_id
                    WHERE s.user_id=u.id
                    ORDER BY CASE WHEN s.status='active' THEN 0 ELSE 1 END,s.created_at DESC
                    LIMIT 1
                ) sub ON TRUE
                WHERE @query='' OR u.email ILIKE '%' || @query || '%' OR COALESCE(u.display_name,'') ILIKE '%' || @query || '%'
                ORDER BY CASE u.status WHEN 'pending' THEN 0 WHEN 'active' THEN 1 ELSE 2 END,u.created_at DESC
                LIMIT @pageSize OFFSET @offset;
                """;
            var users = new List<AdminUserSummary>();
            await using (var command = new NpgsqlCommand(usersSql, connection))
            {
                command.Parameters.AddWithValue("query", query);
                command.Parameters.AddWithValue("pageSize", pageSize);
                command.Parameters.AddWithValue("offset", offset);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    users.Add(new AdminUserSummary(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetBoolean(3),
                        reader.GetBoolean(4),
                        reader.GetString(5),
                        reader.GetFieldValue<string[]>(6),
                        reader.GetString(7),
                        reader.GetString(8),
                        reader.GetInt64(9),
                        reader.GetFieldValue<DateTimeOffset>(10),
                        reader.GetFieldValue<DateTimeOffset>(11)));
                }
            }

            const string rolesSql = "SELECT code,name,description FROM roles ORDER BY CASE WHEN code='platform_admin' THEN 1 ELSE 0 END,name;";
            var roles = new List<AdminRoleSummary>();
            await using (var command = new NpgsqlCommand(rolesSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                    roles.Add(new AdminRoleSummary(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }

            return Results.Ok(new { items = users, roles, page, pageSize, total });
        });

        app.MapGet("/api/admin/users/{targetUserId:guid}/audit", async (
            Guid targetUserId, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var limit = ParseBoundedInt(request.Query["limit"], 50, 1, 200);

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            const string sql = """
                SELECT e.id,e.event_type,e.reason,e.created_at,e.actor_user_id,
                       actor.email,actor.display_name
                FROM membership_audit_events e
                LEFT JOIN app_users actor ON actor.id=e.actor_user_id
                WHERE e.user_id=@targetUserId
                ORDER BY e.created_at DESC
                LIMIT @limit;
                """;
            var events = new List<AdminAuditSummary>();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("targetUserId", targetUserId);
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                events.Add(new AdminAuditSummary(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetFieldValue<DateTimeOffset>(3),
                    reader.IsDBNull(4) ? null : reader.GetGuid(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
            return Results.Ok(new { items = events });
        });

        app.MapPut("/api/admin/users/{targetUserId:guid}/status", async (
            Guid targetUserId, AdminUserStatusRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var actor = authorization.Actor!;
            var desiredStatus = body.Status?.Trim().ToLowerInvariant();
            if (desiredStatus is not ("active" or "suspended"))
                return Results.BadRequest(new { error = "Status must be active or suspended." });
            if (actor.UserId == targetUserId && desiredStatus == "suspended")
                return Results.BadRequest(new { error = "You cannot suspend your own System Administrator account." });

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await AcquireMutationLockAsync(connection, transaction, ct);

            var target = await GetTargetForUpdateAsync(connection, transaction, targetUserId, ct);
            if (target is null) return Results.NotFound(new { error = "User account was not found." });
            if (desiredStatus == "active" && !target.IdentityVerified)
                return Results.BadRequest(new { error = "Verify the account identity before approving access." });
            if (target.Status == desiredStatus)
                return Results.Ok(new { updated = false, targetUserId, status = desiredStatus });

            if (target.IsPlatformAdmin && target.Status == "active" && desiredStatus == "suspended")
            {
                if (!await HasAnotherActiveAdministratorAsync(connection, transaction, targetUserId, ct))
                    return Results.Conflict(new { error = "At least one active verified System Administrator must remain." });
            }

            const string updateSql = "UPDATE app_users SET status=@status,updated_at=NOW() WHERE id=@targetUserId;";
            await using (var update = new NpgsqlCommand(updateSql, connection, transaction))
            {
                update.Parameters.AddWithValue("status", desiredStatus);
                update.Parameters.AddWithValue("targetUserId", targetUserId);
                await update.ExecuteNonQueryAsync(ct);
            }

            var revokedSessions = 0;
            if (desiredStatus == "suspended" || (target.Status == "pending" && desiredStatus == "active"))
                revokedSessions = await RevokeSessionsAsync(connection, transaction, targetUserId, ct);

            var eventType = desiredStatus == "active" && target.Status == "pending"
                ? "account_approved"
                : desiredStatus == "active" ? "account_reactivated" : "account_suspended";
            var reason = NormalizeReason(body.Reason, $"System Administrator changed account status from '{target.Status}' to '{desiredStatus}'.");
            await RecordAuditAsync(connection, transaction, targetUserId, actor.UserId, eventType, reason, ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { updated = true, targetUserId, status = desiredStatus, revokedSessions });
        });

        app.MapPut("/api/admin/users/{targetUserId:guid}/roles", async (
            Guid targetUserId, AdminUserRolesRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var actor = authorization.Actor!;
            var desiredRoles = (body.Roles ?? Array.Empty<string>())
                .Select(role => role.Trim().ToLowerInvariant())
                .Where(role => role.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await AcquireMutationLockAsync(connection, transaction, ct);

            var target = await GetTargetForUpdateAsync(connection, transaction, targetUserId, ct);
            if (target is null) return Results.NotFound(new { error = "User account was not found." });

            var knownRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var roleCatalog = new NpgsqlCommand("SELECT code FROM roles;", connection, transaction))
            await using (var reader = await roleCatalog.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct)) knownRoles.Add(reader.GetString(0));
            }
            var unknown = desiredRoles.Where(role => !knownRoles.Contains(role)).ToArray();
            if (unknown.Length > 0)
                return Results.BadRequest(new { error = $"Unknown role code(s): {string.Join(", ", unknown)}." });

            var currentRoles = await GetRolesAsync(connection, transaction, targetUserId, ct);
            var removesPlatformAdmin = currentRoles.Contains("platform_admin") && !desiredRoles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase);
            if (actor.UserId == targetUserId && removesPlatformAdmin)
                return Results.BadRequest(new { error = "You cannot remove your own System Administrator role." });
            if (removesPlatformAdmin && target.Status == "active" && target.IdentityVerified &&
                !await HasAnotherActiveAdministratorAsync(connection, transaction, targetUserId, ct))
                return Results.Conflict(new { error = "At least one active verified System Administrator must remain." });
            if (desiredRoles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase) &&
                (target.Status != "active" || !target.IdentityVerified))
                return Results.BadRequest(new { error = "Only active verified accounts can become System Administrators." });

            await using (var delete = new NpgsqlCommand("DELETE FROM user_roles WHERE user_id=@targetUserId;", connection, transaction))
            {
                delete.Parameters.AddWithValue("targetUserId", targetUserId);
                await delete.ExecuteNonQueryAsync(ct);
            }
            if (desiredRoles.Length > 0)
            {
                const string insertSql = """
                    INSERT INTO user_roles (user_id,role_id)
                    SELECT @targetUserId,id FROM roles WHERE code=ANY(@roles);
                    """;
                await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
                insert.Parameters.AddWithValue("targetUserId", targetUserId);
                insert.Parameters.AddWithValue("roles", desiredRoles);
                await insert.ExecuteNonQueryAsync(ct);
            }

            var reason = NormalizeReason(body.Reason,
                $"System Administrator replaced roles [{string.Join(", ", currentRoles)}] with [{string.Join(", ", desiredRoles)}].");
            await RecordAuditAsync(connection, transaction, targetUserId, actor.UserId, "roles_updated", reason, ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { updated = true, targetUserId, roles = desiredRoles });
        });

        app.MapPost("/api/admin/users/{targetUserId:guid}/sessions/revoke", async (
            Guid targetUserId, AdminSessionRevokeRequest? body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var actor = authorization.Actor!;

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            var exists = await new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM app_users WHERE id=@targetUserId);", connection, transaction)
            {
                Parameters = { new("targetUserId", targetUserId) }
            }.ExecuteScalarAsync(ct);
            if (exists is not true) return Results.NotFound(new { error = "User account was not found." });

            var revokedSessions = await RevokeSessionsAsync(connection, transaction, targetUserId, ct);
            await RecordAuditAsync(connection, transaction, targetUserId, actor.UserId, "sessions_revoked",
                NormalizeReason(body?.Reason, $"System Administrator revoked {revokedSessions} active session(s)."), ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { revokedSessions });
        });

        return app;
    }

    private static async Task<AdminAuthorization> AuthorizeAsync(
        HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return new(null, Results.Problem("Administrative user management requires PostgreSQL.", statusCode: 503));

        var token = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(token)) return new(null, Results.Unauthorized());
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return new(null, Results.Unauthorized());

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            SELECT EXISTS(
                SELECT 1
                FROM app_users u
                JOIN user_roles ur ON ur.user_id=u.id
                JOIN roles r ON r.id=ur.role_id
                WHERE u.id=@userId AND u.status='active'
                  AND (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL)
                  AND r.code='platform_admin'
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId.Value);
        return await command.ExecuteScalarAsync(ct) is true
            ? new(new AdminActor(userId.Value), null)
            : new(null, Results.Forbid());
    }

    private static async Task AcquireMutationLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lockId);", connection, transaction);
        command.Parameters.AddWithValue("lockId", AdministratorMutationLock);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<AdminTarget?> GetTargetForUpdateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid targetUserId, CancellationToken ct)
    {
        const string sql = """
            SELECT u.status,
                   (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL) AS identity_verified,
                   EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.id=ur.role_id
                          WHERE ur.user_id=u.id AND r.code='platform_admin') AS platform_admin
            FROM app_users u WHERE u.id=@targetUserId FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetUserId", targetUserId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new AdminTarget(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : null;
    }

    private static async Task<bool> HasAnotherActiveAdministratorAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid excludedUserId, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM app_users u
                JOIN user_roles ur ON ur.user_id=u.id
                JOIN roles r ON r.id=ur.role_id
                WHERE r.code='platform_admin' AND u.id<>@excludedUserId AND u.status='active'
                  AND (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL)
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("excludedUserId", excludedUserId);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<string[]> GetRolesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid targetUserId, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(array_agg(r.code ORDER BY r.code),'{}'::text[])
            FROM user_roles ur JOIN roles r ON r.id=ur.role_id WHERE ur.user_id=@targetUserId;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetUserId", targetUserId);
        return (string[])(await command.ExecuteScalarAsync(ct) ?? Array.Empty<string>());
    }

    private static async Task<int> RevokeSessionsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid targetUserId, CancellationToken ct)
    {
        const string sql = """
            UPDATE auth_sessions SET revoked_at=NOW()
            WHERE user_id=@targetUserId AND revoked_at IS NULL AND expires_at>NOW();
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetUserId", targetUserId);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task RecordAuditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid targetUserId, Guid actorUserId,
        string eventType, string reason, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO membership_audit_events (user_id,actor_user_id,event_type,reason)
            VALUES (@targetUserId,@actorUserId,@eventType,@reason);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetUserId", targetUserId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("reason", reason);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static int ParseBoundedInt(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;

    private static string NormalizeReason(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }

    private sealed record AdminAuthorization(AdminActor? Actor, IResult? Error);
    private sealed record AdminActor(Guid UserId);
    private sealed record AdminTarget(string Status, bool IdentityVerified, bool IsPlatformAdmin);
}

public sealed record AdminUserStatusRequest(string Status, string? Reason = null);
public sealed record AdminUserRolesRequest(string[]? Roles, string? Reason = null);
public sealed record AdminSessionRevokeRequest(string? Reason = null);
public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string? DisplayName,
    bool EmailVerified,
    bool IdentityVerified,
    string Status,
    IReadOnlyList<string> Roles,
    string PlanCode,
    string SubscriptionStatus,
    long ActiveSessions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public sealed record AdminRoleSummary(string Code, string Name, string? Description);
public sealed record AdminAuditSummary(
    Guid Id,
    string EventType,
    string? Reason,
    DateTimeOffset CreatedAt,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorDisplayName);
