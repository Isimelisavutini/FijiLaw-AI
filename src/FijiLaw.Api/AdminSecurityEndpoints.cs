using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public static class AdminSecurityEndpoints
{
    public static WebApplication MapAdminSecurityEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapGet("/api/admin/security", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(request, context, databaseUrl, ct);
            if (authorization is not null) return authorization;

            var days = ParseBoundedInt(request.Query["days"], 30, 1, 90);
            var page = ParseBoundedInt(request.Query["page"], 1, 1, 10_000);
            var pageSize = ParseBoundedInt(request.Query["pageSize"], 50, 1, 100);
            var eventType = request.Query["eventType"].ToString().Trim().ToLowerInvariant();
            var query = request.Query["q"].ToString().Trim();
            if (query.Length > 80) return Results.BadRequest(new { error = "Audit search is limited to 80 characters." });
            var offset = ((long)page - 1) * pageSize;

            await using var connection = new NpgsqlConnection(databaseUrl!);
            await connection.OpenAsync(ct);

            const string postureSql = """
                SELECT
                  (SELECT COUNT(*) FROM app_users),
                  (SELECT COUNT(*) FROM app_users WHERE status='pending'),
                  (SELECT COUNT(*) FROM app_users WHERE status='suspended'),
                  (SELECT COUNT(*) FROM app_users
                   WHERE status<>'suspended'
                     AND NOT (email_verified OR phone_verified OR identity_verified_at IS NOT NULL)),
                  (SELECT COUNT(*) FROM auth_sessions
                   WHERE revoked_at IS NULL AND expires_at>NOW()),
                  (SELECT COUNT(DISTINCT user_id) FROM auth_sessions
                   WHERE revoked_at IS NULL AND expires_at>NOW()),
                  (SELECT COUNT(*) FROM membership_audit_events
                   WHERE created_at>=NOW()-INTERVAL '24 hours'),
                  (SELECT COUNT(*) FROM membership_audit_events
                   WHERE created_at>=NOW()-(@days * INTERVAL '1 day')),
                  (SELECT COUNT(DISTINCT u.id) FROM app_users u
                   JOIN user_roles ur ON ur.user_id=u.id
                   JOIN roles r ON r.id=ur.role_id
                   WHERE r.code='platform_admin' AND u.status='active'
                     AND (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL));
                """;
            AdminSecurityPosture posture;
            await using (var command = new NpgsqlCommand(postureSql, connection))
            {
                command.Parameters.AddWithValue("days", days);
                await using var reader = await command.ExecuteReaderAsync(ct);
                await reader.ReadAsync(ct);
                posture = new AdminSecurityPosture(
                    reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                    reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8));
            }

            const string topEventsSql = """
                SELECT event_type,COUNT(*)
                FROM membership_audit_events
                WHERE created_at>=NOW()-(@days * INTERVAL '1 day')
                GROUP BY event_type
                ORDER BY COUNT(*) DESC,event_type
                LIMIT 8;
                """;
            var topEvents = new List<AdminSecurityEventCount>();
            await using (var command = new NpgsqlCommand(topEventsSql, connection))
            {
                command.Parameters.AddWithValue("days", days);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    topEvents.Add(new AdminSecurityEventCount(reader.GetString(0), reader.GetInt64(1)));
            }

            const string dailySql = """
                SELECT series.day::date,COUNT(events.id)
                FROM generate_series(
                    CURRENT_DATE-((@days-1) * INTERVAL '1 day'),
                    CURRENT_DATE,
                    INTERVAL '1 day'
                ) AS series(day)
                LEFT JOIN membership_audit_events events
                  ON events.created_at>=series.day AND events.created_at<series.day+INTERVAL '1 day'
                GROUP BY series.day
                ORDER BY series.day;
                """;
            var daily = new List<AdminSecurityDailyCount>();
            await using (var command = new NpgsqlCommand(dailySql, connection))
            {
                command.Parameters.AddWithValue("days", days);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    daily.Add(new AdminSecurityDailyCount(reader.GetDateTime(0).ToString("yyyy-MM-dd"), reader.GetInt64(1)));
            }

            const string eventTypesSql = "SELECT DISTINCT event_type FROM membership_audit_events ORDER BY event_type;";
            var eventTypes = new List<string>();
            await using (var command = new NpgsqlCommand(eventTypesSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct)) eventTypes.Add(reader.GetString(0));
            }

            const string filterSql = """
                FROM membership_audit_events events
                LEFT JOIN app_users target ON target.id=events.user_id
                LEFT JOIN app_users actor ON actor.id=events.actor_user_id
                WHERE events.created_at>=NOW()-(@days * INTERVAL '1 day')
                  AND (@eventType='' OR events.event_type=@eventType)
                  AND (
                    @query='' OR events.event_type ILIKE @query || '%'
                    OR to_tsvector('simple',COALESCE(events.reason,'')) @@ plainto_tsquery('simple',@query)
                    OR COALESCE(target.email,'') ILIKE @query || '%'
                    OR COALESCE(target.display_name,'') ILIKE @query || '%'
                    OR COALESCE(actor.email,'') ILIKE @query || '%'
                    OR COALESCE(actor.display_name,'') ILIKE @query || '%'
                  )
                """;
            long total;
            await using (var command = new NpgsqlCommand("SELECT COUNT(*) " + filterSql, connection))
            {
                AddFilterParameters(command, days, eventType, query);
                total = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            }

            var events = new List<AdminSecurityAuditEvent>();
            var eventsSql = """
                SELECT events.id,events.event_type,events.reason,events.created_at,
                       events.user_id,target.email,target.display_name,
                       events.actor_user_id,actor.email,actor.display_name
                """ + filterSql + " ORDER BY events.created_at DESC LIMIT @pageSize OFFSET @offset;";
            await using (var command = new NpgsqlCommand(eventsSql, connection))
            {
                AddFilterParameters(command, days, eventType, query);
                command.Parameters.AddWithValue("pageSize", pageSize);
                command.Parameters.AddWithValue("offset", offset);
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    events.Add(new AdminSecurityAuditEvent(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetFieldValue<DateTimeOffset>(3),
                        reader.IsDBNull(4) ? null : reader.GetGuid(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetString(6),
                        reader.IsDBNull(7) ? null : reader.GetGuid(7),
                        reader.IsDBNull(8) ? null : reader.GetString(8),
                        reader.IsDBNull(9) ? null : reader.GetString(9)));
                }
            }

            return Results.Ok(new
            {
                posture,
                days,
                topEvents,
                daily,
                eventTypes,
                audit = new { items = events, page, pageSize, total }
            });
        }).RequireRateLimiting("admin-read");

        return app;
    }

    private static async Task<IResult?> AuthorizeAsync(
        HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return Results.Problem("Security audit reporting requires PostgreSQL.", statusCode: 503);

        var token = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return Results.Unauthorized();

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
        return await command.ExecuteScalarAsync(ct) is true ? null : Results.Forbid();
    }

    private static void AddFilterParameters(NpgsqlCommand command, int days, string eventType, string query)
    {
        command.Parameters.AddWithValue("days", days);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("query", query);
    }

    private static int ParseBoundedInt(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}

public sealed record AdminSecurityPosture(
    long RegisteredUsers,
    long PendingUsers,
    long SuspendedUsers,
    long UnverifiedUsers,
    long ActiveSessions,
    long UsersWithActiveSessions,
    long EventsLast24Hours,
    long EventsInPeriod,
    long ActiveAdministrators);
public sealed record AdminSecurityEventCount(string EventType, long Count);
public sealed record AdminSecurityDailyCount(string Date, long Count);
public sealed record AdminSecurityAuditEvent(
    Guid Id,
    string EventType,
    string? Reason,
    DateTimeOffset CreatedAt,
    Guid? TargetUserId,
    string? TargetEmail,
    string? TargetDisplayName,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorDisplayName);
