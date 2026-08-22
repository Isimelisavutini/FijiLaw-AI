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
                var result = await store.RegisterAsync(request, ct);
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                await security.RecordAuditAsync(result.UserId, result.UserId, "member_registered", "Member account created", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Member sign-in is not available until PostgreSQL is connected.", statusCode: 503);
            try
            {
                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                var result = await store.LoginAsync(request, ct);
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                await security.RecordAuditAsync(result.UserId, result.UserId, "member_login", "Member signed in", ct);
                return Results.Ok(result);
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

        app.MapPost("/api/auth/request-email-verification", async (EmailVerificationRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Email verification is not available until PostgreSQL is connected.", statusCode: 503);
            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var created = await security.CreateVerificationTokenAsync(request.Email, ct);
            if (created is not null)
                await security.RecordAuditAsync(created.Value.UserId, created.Value.UserId, "email_verification_requested", "Verification token issued", ct);

            // Do not reveal whether the email exists. Delivery is intentionally separated from token issuance.
            return Results.Accepted(value: new { message = "If the account exists and is not yet verified, a verification request has been created.", deliveryConfigured = false });
        });

        app.MapPost("/api/auth/verify-email", async (EmailVerificationConfirmRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Email verification is not available until PostgreSQL is connected.", statusCode: 503);
            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var userId = await security.VerifyEmailAsync(request.Token, ct);
            return userId is null ? Results.BadRequest(new { error = "The verification token is invalid or expired." }) : Results.Ok(new { verified = true });
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
            if (!member.EmailVerified)
                return Results.Json(new { error = "Verify your email before accessing paid member features." }, statusCode: 403);
            if (!member.Permissions.Contains(MembershipPermissions.DashboardAccess, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "A paid membership is required to access the FijiLaw dashboard.", planCode = member.PlanCode }, statusCode: 403);

            return Results.Ok(new DashboardSummary(member.UserId, member.Email, member.DisplayName, member.PlanCode,
                member.SubscriptionStatus, member.Roles, member.Permissions, true));
        });

        app.MapGet("/api/authz/{permission}", async (string permission, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            var allowed = member.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(allowed ? AuthorizationDecision.Allow() : AuthorizationDecision.Deny("Permission not granted by active role or subscription."));
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
        await using var command = new Npgsql.NpgsqlCommand("SELECT email,display_name,email_verified FROM app_users WHERE id=@id AND status='active'", connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var email = reader.GetString(0);
        var displayName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var emailVerified = reader.GetBoolean(2);
        return new AuthenticatedMember(userId.Value, email, displayName, emailVerified, access.Roles, access.Permissions, access.PlanCode, access.SubscriptionStatus, access.CurrentPeriodEnd);
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
