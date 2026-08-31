using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public static class AdminMembershipEndpoints
{
    private const long MembershipMutationLock = 726_452_102;

    public static WebApplication MapAdminMembershipEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapGet("/api/admin/memberships", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;

            var page = ParseBoundedInt(request.Query["page"], 1, 1, 10_000);
            var pageSize = ParseBoundedInt(request.Query["pageSize"], 50, 1, 100);
            var query = request.Query["q"].ToString().Trim();
            var status = request.Query["status"].ToString().Trim().ToLowerInvariant();
            var planCode = request.Query["planCode"].ToString().Trim().ToLowerInvariant();
            if (query.Length > 80) return Results.BadRequest(new { error = "Membership search is limited to 80 characters." });
            var offset = ((long)page - 1) * pageSize;

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);

            const string summarySql = """
                SELECT
                  COUNT(*) FILTER (WHERE s.status IN ('active','trialing') AND (s.current_period_end IS NULL OR s.current_period_end>NOW())),
                  COUNT(*) FILTER (WHERE s.status IN ('active','trialing') AND plans.is_paid
                    AND (s.current_period_end IS NULL OR s.current_period_end>NOW())),
                  COUNT(*) FILTER (WHERE s.status IN ('active','trialing') AND NOT plans.is_paid
                    AND (s.current_period_end IS NULL OR s.current_period_end>NOW())),
                  COUNT(*) FILTER (WHERE s.status IN ('active','trialing') AND s.billing_provider='administrator'
                    AND (s.current_period_end IS NULL OR s.current_period_end>NOW())),
                  COUNT(*) FILTER (WHERE s.status IN ('active','trialing') AND s.current_period_end BETWEEN NOW() AND NOW()+INTERVAL '30 days'),
                  COALESCE(SUM(
                    CASE
                      WHEN s.status NOT IN ('active','trialing') OR s.billing_provider='administrator'
                        OR (s.current_period_end IS NOT NULL AND s.current_period_end<=NOW()) THEN 0
                      WHEN s.billing_interval='monthly' THEN COALESCE(plans.monthly_price_fjd,0)
                      WHEN s.billing_interval='annual' THEN COALESCE(plans.annual_price_fjd,0)/12
                      ELSE 0
                    END
                  ),0)
                FROM subscriptions s
                JOIN subscription_plans plans ON plans.id=s.plan_id;
                """;
            AdminMembershipSummary summary;
            await using (var command = new NpgsqlCommand(summarySql, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                await reader.ReadAsync(ct);
                summary = new AdminMembershipSummary(
                    reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
                    reader.GetInt64(3), reader.GetInt64(4), reader.GetDecimal(5));
            }

            const string plansSql = """
                SELECT plans.code,plans.name,plans.audience,plans.monthly_price_fjd,plans.annual_price_fjd,plans.is_paid,
                       COUNT(subscriptions.id) FILTER (
                         WHERE subscriptions.status IN ('active','trialing')
                           AND (subscriptions.current_period_end IS NULL OR subscriptions.current_period_end>NOW())
                       ) AS active_members,
                       COUNT(subscriptions.id) FILTER (
                         WHERE subscriptions.status IN ('active','trialing')
                           AND subscriptions.billing_provider='administrator'
                           AND (subscriptions.current_period_end IS NULL OR subscriptions.current_period_end>NOW())
                       ) AS manual_grants
                FROM subscription_plans plans
                LEFT JOIN subscriptions ON subscriptions.plan_id=plans.id
                WHERE plans.is_active=TRUE
                GROUP BY plans.id
                ORDER BY plans.sort_order;
                """;
            var plans = new List<AdminMembershipPlan>();
            await using (var command = new NpgsqlCommand(plansSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    plans.Add(new AdminMembershipPlan(
                        reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                        reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                        reader.GetBoolean(5), reader.GetInt64(6), reader.GetInt64(7)));
                }
            }

            const string demandSql = """
                SELECT COALESCE(requested_plan_code,'free'),COUNT(*)
                FROM app_users
                GROUP BY COALESCE(requested_plan_code,'free')
                ORDER BY COUNT(*) DESC;
                """;
            var requestedPlans = new List<AdminRequestedPlanCount>();
            await using (var command = new NpgsqlCommand(demandSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                    requestedPlans.Add(new AdminRequestedPlanCount(reader.GetString(0), reader.GetInt64(1)));
            }

            const string membershipFromSql = """
                FROM app_users users
                LEFT JOIN LATERAL (
                    SELECT subscriptions.id,plans.code AS plan_code,plans.name AS plan_name,plans.is_paid,
                           subscriptions.status,subscriptions.billing_provider,subscriptions.billing_interval,
                           subscriptions.current_period_end,subscriptions.created_at
                    FROM subscriptions
                    JOIN subscription_plans plans ON plans.id=subscriptions.plan_id
                    WHERE subscriptions.user_id=users.id
                    ORDER BY
                      CASE WHEN subscriptions.status IN ('active','trialing')
                                AND (subscriptions.current_period_end IS NULL OR subscriptions.current_period_end>NOW())
                           THEN 0 ELSE 1 END,
                      subscriptions.created_at DESC
                    LIMIT 1
                ) membership ON TRUE
                WHERE (@query='' OR users.email ILIKE @query || '%' OR COALESCE(users.display_name,'') ILIKE @query || '%')
                  AND (@status='' OR COALESCE(membership.status,'none')=@status)
                  AND (@planCode='' OR COALESCE(membership.plan_code,'free')=@planCode)
                """;
            long total;
            await using (var command = new NpgsqlCommand("SELECT COUNT(*) " + membershipFromSql, connection))
            {
                AddListParameters(command, query, status, planCode);
                total = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            }

            var memberships = new List<AdminMembershipAccount>();
            var listSql = """
                SELECT users.id,users.email,users.display_name,users.status,
                       (users.email_verified OR users.phone_verified OR users.identity_verified_at IS NOT NULL) AS identity_verified,
                       COALESCE(users.requested_plan_code,'free'),
                       membership.id,membership.plan_code,membership.plan_name,membership.is_paid,
                       membership.status,membership.billing_provider,membership.billing_interval,
                       membership.current_period_end,membership.created_at
                """ + membershipFromSql + " ORDER BY users.created_at DESC LIMIT @pageSize OFFSET @offset;";
            await using (var command = new NpgsqlCommand(listSql, connection))
            {
                AddListParameters(command, query, status, planCode);
                command.Parameters.AddWithValue("pageSize", pageSize);
                command.Parameters.AddWithValue("offset", offset);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    memberships.Add(new AdminMembershipAccount(
                        reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetString(3), reader.GetBoolean(4), reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetGuid(6),
                        reader.IsDBNull(7) ? "free" : reader.GetString(7),
                        reader.IsDBNull(8) ? "Free" : reader.GetString(8),
                        !reader.IsDBNull(9) && reader.GetBoolean(9),
                        reader.IsDBNull(10) ? "none" : reader.GetString(10),
                        reader.IsDBNull(11) ? null : reader.GetString(11),
                        reader.IsDBNull(12) ? null : reader.GetString(12),
                        reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                        reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14)));
                }
            }

            return Results.Ok(new
            {
                summary,
                plans,
                requestedPlans,
                accounts = new { items = memberships, page, pageSize, total }
            });
        }).RequireRateLimiting("admin-read");

        app.MapPut("/api/admin/memberships/users/{targetUserId:guid}/grant", async (
            Guid targetUserId, AdminMembershipGrantRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var actor = authorization.Actor!;
            var planCode = body.PlanCode?.Trim().ToLowerInvariant() ?? "";
            var reason = body.Reason?.Trim() ?? "";
            if (reason.Length < 8 || reason.Length > 500)
                return Results.BadRequest(new { error = "A reason between 8 and 500 characters is required." });
            if (body.ExpiresAt is null || body.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5) ||
                body.ExpiresAt > DateTimeOffset.UtcNow.AddDays(366))
                return Results.BadRequest(new { error = "Manual paid access must expire between five minutes and one year from now." });

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await AcquireMutationLockAsync(connection, transaction, ct);

            const string targetSql = """
                SELECT status,(email_verified OR phone_verified OR identity_verified_at IS NOT NULL)
                FROM app_users WHERE id=@targetUserId FOR UPDATE;
                """;
            string? targetStatus = null;
            var targetVerified = false;
            await using (var command = new NpgsqlCommand(targetSql, connection, transaction))
            {
                command.Parameters.AddWithValue("targetUserId", targetUserId);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    targetStatus = reader.GetString(0);
                    targetVerified = reader.GetBoolean(1);
                }
            }
            if (targetStatus is null) return Results.NotFound(new { error = "User account was not found." });
            if (targetStatus != "active" || !targetVerified)
                return Results.BadRequest(new { error = "Manual membership access requires an active verified account." });

            Guid? planId = null;
            string? planName = null;
            const string planSql = "SELECT id,name FROM subscription_plans WHERE code=@planCode AND is_active=TRUE AND is_paid=TRUE;";
            await using (var command = new NpgsqlCommand(planSql, connection, transaction))
            {
                command.Parameters.AddWithValue("planCode", planCode);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    planId = reader.GetGuid(0);
                    planName = reader.GetString(1);
                }
            }
            if (planId is null) return Results.BadRequest(new { error = "Choose an active paid membership plan." });

            const string deactivateSql = """
                UPDATE subscriptions
                SET status='inactive',cancelled_at=NOW(),updated_at=NOW()
                WHERE user_id=@targetUserId AND billing_provider='administrator'
                  AND status IN ('active','trialing');
                """;
            await using (var command = new NpgsqlCommand(deactivateSql, connection, transaction))
            {
                command.Parameters.AddWithValue("targetUserId", targetUserId);
                await command.ExecuteNonQueryAsync(ct);
            }

            var subscriptionId = Guid.NewGuid();
            const string insertSql = """
                INSERT INTO subscriptions
                  (id,user_id,plan_id,billing_provider,status,billing_interval,current_period_start,current_period_end,started_at)
                VALUES
                  (@id,@targetUserId,@planId,'administrator','active','manual',NOW(),@expiresAt,NOW());
                """;
            await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", subscriptionId);
                command.Parameters.AddWithValue("targetUserId", targetUserId);
                command.Parameters.AddWithValue("planId", planId.Value);
                command.Parameters.AddWithValue("expiresAt", body.ExpiresAt.Value);
                await command.ExecuteNonQueryAsync(ct);
            }

            await RecordAuditAsync(connection, transaction, targetUserId, actor.UserId, "membership_grant_created",
                $"Administrator granted '{planCode}' access until {body.ExpiresAt.Value:O}. Reason: {reason}", ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { granted = true, subscriptionId, targetUserId, planCode, planName, expiresAt = body.ExpiresAt });
        }).RequireRateLimiting("admin-write");

        app.MapPost("/api/admin/memberships/users/{targetUserId:guid}/grant/revoke", async (
            Guid targetUserId, AdminMembershipRevokeRequest body, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization.Error is not null) return authorization.Error;
            var actor = authorization.Actor!;
            var reason = body.Reason?.Trim() ?? "";
            if (reason.Length < 8 || reason.Length > 500)
                return Results.BadRequest(new { error = "A reason between 8 and 500 characters is required." });

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await AcquireMutationLockAsync(connection, transaction, ct);

            const string revokeSql = """
                UPDATE subscriptions
                SET status='cancelled',cancelled_at=NOW(),current_period_end=LEAST(COALESCE(current_period_end,NOW()),NOW()),updated_at=NOW()
                WHERE user_id=@targetUserId AND billing_provider='administrator'
                  AND status IN ('active','trialing')
                  AND (current_period_end IS NULL OR current_period_end>NOW());
                """;
            int revoked;
            await using (var command = new NpgsqlCommand(revokeSql, connection, transaction))
            {
                command.Parameters.AddWithValue("targetUserId", targetUserId);
                revoked = await command.ExecuteNonQueryAsync(ct);
            }
            if (revoked == 0) return Results.Conflict(new { error = "This account has no active administrator grant." });

            await RecordAuditAsync(connection, transaction, targetUserId, actor.UserId, "membership_grant_revoked",
                $"Administrator revoked {revoked} manual membership grant(s). Reason: {reason}", ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { revoked = true, targetUserId, revokedGrants = revoked });
        }).RequireRateLimiting("admin-write");

        return app;
    }

    private static async Task<AdminMembershipAuthorization> AuthorizeAsync(
        HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return new(null, Results.Problem("Membership administration requires PostgreSQL.", statusCode: 503));
        var token = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(token)) return new(null, Results.Unauthorized());
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return new(null, Results.Unauthorized());

        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM app_users users
                JOIN user_roles user_roles ON user_roles.user_id=users.id
                JOIN roles roles ON roles.id=user_roles.role_id
                WHERE users.id=@userId AND users.status='active' AND roles.code='platform_admin'
                  AND (users.email_verified OR users.phone_verified OR users.identity_verified_at IS NOT NULL)
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId.Value);
        return await command.ExecuteScalarAsync(ct) is true
            ? new(new AdminMembershipActor(userId.Value), null)
            : new(null, Results.Forbid());
    }

    private static async Task AcquireMutationLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lockId);", connection, transaction);
        command.Parameters.AddWithValue("lockId", MembershipMutationLock);
        await command.ExecuteNonQueryAsync(ct);
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

    private static void AddListParameters(NpgsqlCommand command, string query, string status, string planCode)
    {
        command.Parameters.AddWithValue("query", query);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("planCode", planCode);
    }

    private static int ParseBoundedInt(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }

    private sealed record AdminMembershipAuthorization(AdminMembershipActor? Actor, IResult? Error);
    private sealed record AdminMembershipActor(Guid UserId);
}

public sealed record AdminMembershipGrantRequest(string? PlanCode, DateTimeOffset? ExpiresAt, string? Reason);
public sealed record AdminMembershipRevokeRequest(string? Reason);
public sealed record AdminMembershipSummary(
    long ActiveSubscriptions,
    long ActivePaidSubscriptions,
    long ActiveFreeSubscriptions,
    long ActiveManualGrants,
    long ExpiringWithin30Days,
    decimal MonthlyRecurringRevenueFjd);
public sealed record AdminMembershipPlan(
    string Code,
    string Name,
    string Audience,
    decimal? MonthlyPriceFjd,
    decimal? AnnualPriceFjd,
    bool IsPaid,
    long ActiveMembers,
    long ManualGrants);
public sealed record AdminRequestedPlanCount(string PlanCode, long Count);
public sealed record AdminMembershipAccount(
    Guid UserId,
    string Email,
    string? DisplayName,
    string AccountStatus,
    bool IdentityVerified,
    string RequestedPlanCode,
    Guid? SubscriptionId,
    string PlanCode,
    string PlanName,
    bool IsPaid,
    string SubscriptionStatus,
    string? BillingProvider,
    string? BillingInterval,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset? SubscriptionCreatedAt);
