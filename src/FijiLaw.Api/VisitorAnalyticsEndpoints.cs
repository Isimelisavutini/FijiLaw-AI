using System.Security.Cryptography;
using System.Text;
using FijiLaw.Infrastructure;
using Npgsql;

namespace FijiLaw.Api;

public sealed class PostgresVisitorAnalyticsStore
{
    private readonly string _connectionString;
    public PostgresVisitorAnalyticsStore(string connectionString) => _connectionString = connectionString;

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS visitor_profiles (
            visitor_hash TEXT PRIMARY KEY,
            first_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            first_user_id UUID NULL REFERENCES app_users(id) ON DELETE SET NULL,
            last_user_id UUID NULL REFERENCES app_users(id) ON DELETE SET NULL,
            total_page_views BIGINT NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS page_visit_events (
            id BIGSERIAL PRIMARY KEY,
            visitor_hash TEXT NOT NULL REFERENCES visitor_profiles(visitor_hash) ON DELETE CASCADE,
            user_id UUID NULL REFERENCES app_users(id) ON DELETE SET NULL,
            path TEXT NOT NULL,
            referrer_host TEXT NULL,
            device_type TEXT NOT NULL,
            browser_family TEXT NOT NULL,
            occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS ix_page_visit_events_occurred_at ON page_visit_events(occurred_at DESC);
        CREATE INDEX IF NOT EXISTS ix_page_visit_events_user_id ON page_visit_events(user_id) WHERE user_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_page_visit_events_path ON page_visit_events(path);
        """;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordAsync(string visitorId, Guid? userId, string path, string? referrer, string? userAgent, CancellationToken ct)
    {
        var visitorHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(visitorId.Trim()))).ToLowerInvariant();
        var safePath = NormalizePath(path);
        var referrerHost = ReferrerHost(referrer);
        var device = DeviceType(userAgent);
        var browser = BrowserFamily(userAgent);

        const string sql = """
        INSERT INTO visitor_profiles(visitor_hash, first_user_id, last_user_id, total_page_views)
        VALUES (@visitor_hash, @user_id, @user_id, 1)
        ON CONFLICT (visitor_hash) DO UPDATE SET
            last_seen_at = NOW(),
            last_user_id = COALESCE(@user_id, visitor_profiles.last_user_id),
            total_page_views = visitor_profiles.total_page_views + 1;

        INSERT INTO page_visit_events(visitor_hash, user_id, path, referrer_host, device_type, browser_family)
        VALUES (@visitor_hash, @user_id, @path, @referrer_host, @device_type, @browser_family);
        """;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("visitor_hash", visitorHash);
        command.Parameters.AddWithValue("user_id", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("path", safePath);
        command.Parameters.AddWithValue("referrer_host", (object?)referrerHost ?? DBNull.Value);
        command.Parameters.AddWithValue("device_type", device);
        command.Parameters.AddWithValue("browser_family", browser);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<object> GetSummaryAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var totals = new Dictionary<string,long>();
        const string totalsSql = """
        SELECT
          COUNT(*) AS page_views,
          COUNT(DISTINCT e.visitor_hash) AS unique_visitors,
          COUNT(DISTINCT e.user_id) FILTER (WHERE e.user_id IS NOT NULL) AS signed_in_users,
          COUNT(DISTINCT e.visitor_hash) FILTER (WHERE e.user_id IS NULL) AS guest_visitors,
          COUNT(DISTINCT e.visitor_hash) FILTER (WHERE p.first_seen_at >= NOW() - (@days * INTERVAL '1 day')) AS new_visitors
        FROM page_visit_events e
        JOIN visitor_profiles p ON p.visitor_hash=e.visitor_hash
        WHERE e.occurred_at >= NOW() - (@days * INTERVAL '1 day');
        """;
        await using (var command = new NpgsqlCommand(totalsSql, connection))
        {
            command.Parameters.AddWithValue("days", days);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                totals["pageViews"] = reader.GetInt64(0);
                totals["uniqueVisitors"] = reader.GetInt64(1);
                totals["signedInUsers"] = reader.GetInt64(2);
                totals["guestVisitors"] = reader.GetInt64(3);
                totals["newVisitors"] = reader.GetInt64(4);
                totals["returningVisitors"] = Math.Max(0, totals["uniqueVisitors"] - totals["newVisitors"]);
            }
        }

        var daily = new List<object>();
        const string dailySql = """
        SELECT TO_CHAR(DATE(occurred_at), 'YYYY-MM-DD') AS day, COUNT(*) AS views, COUNT(DISTINCT visitor_hash) AS visitors
        FROM page_visit_events
        WHERE occurred_at >= NOW() - (@days * INTERVAL '1 day')
        GROUP BY DATE(occurred_at)
        ORDER BY DATE(occurred_at);
        """;
        await using (var command = new NpgsqlCommand(dailySql, connection))
        {
            command.Parameters.AddWithValue("days", days);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) daily.Add(new { date = reader.GetString(0), views = reader.GetInt64(1), visitors = reader.GetInt64(2) });
        }

        var topPages = new List<object>();
        const string pageSql = """
        SELECT path, COUNT(*) AS views, COUNT(DISTINCT visitor_hash) AS visitors
        FROM page_visit_events
        WHERE occurred_at >= NOW() - (@days * INTERVAL '1 day')
        GROUP BY path ORDER BY views DESC LIMIT 10;
        """;
        await using (var command = new NpgsqlCommand(pageSql, connection))
        {
            command.Parameters.AddWithValue("days", days);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) topPages.Add(new { path = reader.GetString(0), views = reader.GetInt64(1), visitors = reader.GetInt64(2) });
        }

        var recent = new List<object>();
        const string recentSql = """
        SELECT e.path, e.occurred_at, e.device_type, e.browser_family, e.referrer_host,
               u.email, u.display_name, e.user_id IS NOT NULL AS signed_in
        FROM page_visit_events e
        LEFT JOIN app_users u ON u.id=e.user_id
        ORDER BY e.occurred_at DESC LIMIT 50;
        """;
        await using (var command = new NpgsqlCommand(recentSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) recent.Add(new {
                path = reader.GetString(0),
                occurredAt = reader.GetFieldValue<DateTimeOffset>(1),
                device = reader.GetString(2),
                browser = reader.GetString(3),
                referrer = reader.IsDBNull(4) ? null : reader.GetString(4),
                email = reader.IsDBNull(5) ? null : reader.GetString(5),
                displayName = reader.IsDBNull(6) ? null : reader.GetString(6),
                signedIn = reader.GetBoolean(7)
            });
        }

        var devices = new List<object>();
        const string devicesSql = """
        SELECT device_type, COUNT(*) FROM page_visit_events
        WHERE occurred_at >= NOW() - (@days * INTERVAL '1 day')
        GROUP BY device_type ORDER BY COUNT(*) DESC;
        """;
        await using (var command = new NpgsqlCommand(devicesSql, connection))
        {
            command.Parameters.AddWithValue("days", days);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) devices.Add(new { device = reader.GetString(0), views = reader.GetInt64(1) });
        }

        return new { days, totals, daily, topPages, recent, devices };
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var clean = path.Split('?', '#')[0].Trim();
        if (!clean.StartsWith('/')) clean = "/" + clean;
        return clean.Length > 300 ? clean[..300] : clean;
    }
    private static string? ReferrerHost(string? referrer)
    {
        if (string.IsNullOrWhiteSpace(referrer)) return null;
        return Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;
    }
    private static string DeviceType(string? ua)
    {
        var value = ua?.ToLowerInvariant() ?? "";
        if (value.Contains("ipad") || value.Contains("tablet")) return "tablet";
        if (value.Contains("mobi") || value.Contains("iphone") || value.Contains("android")) return "mobile";
        return "desktop";
    }
    private static string BrowserFamily(string? ua)
    {
        var value = ua?.ToLowerInvariant() ?? "";
        if (value.Contains("edg/")) return "Edge";
        if (value.Contains("chrome/") && !value.Contains("edg/")) return "Chrome";
        if (value.Contains("safari/") && !value.Contains("chrome/")) return "Safari";
        if (value.Contains("firefox/")) return "Firefox";
        return "Other";
    }
}

public sealed record VisitorEventRequest(string VisitorId, string Path, string? Referrer, string? UserAgent);

public static class VisitorAnalyticsEndpoints
{
    public static WebApplication MapVisitorAnalyticsEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapPost("/api/analytics/visit", async (VisitorEventRequest request, HttpRequest httpRequest, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.NoContent();
            if (string.IsNullOrWhiteSpace(request.VisitorId) || request.VisitorId.Length > 200) return Results.BadRequest(new { error = "Invalid visitor identifier." });
            var userId = await ResolveUserIdAsync(httpRequest, context, databaseUrl, ct);
            var store = context.RequestServices.GetRequiredService<PostgresVisitorAnalyticsStore>();
            await store.RecordAsync(request.VisitorId, userId, request.Path, request.Referrer, request.UserAgent, ct);
            return Results.Accepted();
        }).RequireRateLimiting("analytics");

        app.MapGet("/api/admin/analytics/visitors", async (int? days, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Analytics storage is unavailable.", statusCode: 503);
            var userId = await ResolveUserIdAsync(request, context, databaseUrl, ct);
            if (userId is null) return Results.Unauthorized();
            if (!await IsActiveVerifiedAdministratorAsync(databaseUrl, userId.Value, ct)) return Results.Forbid();
            var store = context.RequestServices.GetRequiredService<PostgresVisitorAnalyticsStore>();
            return Results.Ok(await store.GetSummaryAsync(days ?? 30, ct));
        });

        return app;
    }

    private static async Task<bool> IsActiveVerifiedAdministratorAsync(string databaseUrl, Guid userId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM app_users u
                JOIN user_roles ur ON ur.user_id=u.id
                JOIN roles r ON r.id=ur.role_id
                WHERE u.id=@userId AND u.status='active' AND r.code='platform_admin'
                  AND (u.email_verified OR u.phone_verified OR u.identity_verified_at IS NOT NULL)
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<Guid?> ResolveUserIdAsync(HttpRequest request, HttpContext context, string databaseUrl, CancellationToken ct)
    {
        var header = request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        if (string.IsNullOrWhiteSpace(token)) return null;
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        return await auth.ValidateSessionAsync(token, ct);
    }
}
