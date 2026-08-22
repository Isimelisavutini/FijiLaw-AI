using FijiLaw.Domain;
using FijiLaw.Infrastructure;

namespace FijiLaw.Api;

public static class MembershipEndpoints
{
    public static WebApplication MapMembershipEndpoints(this WebApplication app, string? databaseUrl)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Membership registration is not available until PostgreSQL is connected.", statusCode: 503);
            try
            {
                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                return Results.Ok(await store.RegisterAsync(request, ct));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Member sign-in is not available until PostgreSQL is connected.", statusCode: 503);
            try
            {
                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                return Results.Ok(await store.LoginAsync(request, ct));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        app.MapPost("/api/auth/logout", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.NoContent();
            var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
            await store.RevokeSessionAsync(GetBearerToken(request), ct);
            return Results.NoContent();
        });

        app.MapGet("/api/membership/me", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            return member is null ? Results.Unauthorized() : Results.Ok(member);
        });

        app.MapGet("/api/dashboard", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            if (!member.Permissions.Contains(MembershipPermissions.DashboardAccess, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "A paid membership is required to access the FijiLaw dashboard.", planCode = member.PlanCode }, statusCode: 403);

            return Results.Ok(new DashboardSummary(member.UserId, member.Email, member.DisplayName, member.PlanCode,
                member.SubscriptionStatus, member.Roles, member.Permissions, true));
        });

        return app;
    }

    private static async Task<AuthenticatedMember?> ResolveMemberAsync(HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl)) return null;
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(GetBearerToken(request), ct);
        if (userId is null) return null;

        var repository = context.RequestServices.GetRequiredService<PostgresMembershipRepository>();
        var access = await repository.GetAccessAsync(userId.Value, ct);
        if (access is null) return null;

        await using var connection = new Npgsql.NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        await using var command = new Npgsql.NpgsqlCommand("SELECT email, display_name FROM app_users WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var email = reader.GetString(0);
        var displayName = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new AuthenticatedMember(userId.Value, email, displayName, access.Roles, access.Permissions, access.PlanCode, access.SubscriptionStatus, access.CurrentPeriodEnd);
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
