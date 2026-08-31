using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using FijiLaw.Domain;
using FijiLaw.Infrastructure;

namespace FijiLaw.Api;

public static class MembershipEndpoints
{
    public static WebApplication MapMembershipEndpoints(this WebApplication app, string? databaseUrl, string? authBridgeSecret)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                var demo = context.RequestServices.GetRequiredService<DemoMembershipAuthStore>();
                return demo.IsEnabled
                    ? Results.Problem("Registration is disabled while FijiLaw AI is running controlled demo access. Use an administrator-provided demo account. Persistent registration will be enabled when PostgreSQL is connected.", statusCode: 503)
                    : Results.Problem("Membership registration is not available until PostgreSQL is connected.", statusCode: 503);
            }
            try
            {
                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                var result = await store.RegisterAsync(request, ct);
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                await security.RecordAuditAsync(result.UserId, result.UserId, "member_registered", $"Member account created pending System Administrator approval; requested plan '{request.RequestedPlanCode ?? MembershipPlans.Free}'.", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/auth/external-session", async (ExternalIdentitySessionRequest request, HttpRequest httpRequest, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl))
                return Results.Problem("Verified identity registration is not available until PostgreSQL is connected.", statusCode: 503);
            if (string.IsNullOrWhiteSpace(authBridgeSecret))
                return Results.Problem("The verified identity bridge is not configured.", statusCode: 503);
            if (!httpRequest.Headers.TryGetValue("X-Auth-Bridge-Secret", out var suppliedSecret) || !SecretMatches(authBridgeSecret, suppliedSecret.ToString()))
                return Results.Unauthorized();

            try
            {
                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                var result = await store.CreateExternalIdentitySessionAsync(request, ct);
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                var identifierType = request.PhoneVerified ? "phone" : "email";
                await security.RecordAuditAsync(result.UserId, result.UserId, "verified_identity_session", $"Verified {request.IdentityProvider} identity linked using {identifierType} verification.", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(databaseUrl))
                {
                    var demo = context.RequestServices.GetRequiredService<DemoMembershipAuthStore>();
                    if (!demo.IsEnabled) return Results.Problem("Member sign-in is not available until PostgreSQL is connected.", statusCode: 503);
                    return Results.Ok(demo.Login(request));
                }

                var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                var result = await store.LoginAsync(request, ct);
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                await security.RecordAuditAsync(result.UserId, result.UserId, "member_login", "Member signed in", ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                var demo = context.RequestServices.GetRequiredService<DemoMembershipAuthStore>();
                return demo.IsEnabled
                    ? Results.Accepted(value: new { message = "Password recovery is disabled for temporary demo accounts. Contact the FijiLaw administrator for demo credentials.", deliveryConfigured = false, demo = true })
                    : Results.Problem("Password recovery is not available until PostgreSQL is connected.", statusCode: 503);
            }
            var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var emailSender = context.RequestServices.GetRequiredService<ResendEmailSender>();
            var created = await auth.CreatePasswordResetTokenAsync(request.Email, ct);
            if (created is not null)
            {
                await security.RecordAuditAsync(created.Value.UserId, created.Value.UserId, "password_reset_requested", "Password reset token issued", ct);
                if (emailSender.IsConfigured)
                {
                    var sent = await emailSender.SendPasswordResetAsync(created.Value.Email, created.Value.Token, ct);
                    await security.RecordAuditAsync(created.Value.UserId, created.Value.UserId, sent ? "password_reset_email_sent" : "password_reset_email_failed", sent ? "Password reset email accepted by provider" : "Password reset email provider returned a failure", ct);
                }
            }

            return Results.Accepted(value: new
            {
                message = "If an active account exists for that email, password reset instructions have been requested.",
                deliveryConfigured = emailSender.IsConfigured
            });
        }).RequireRateLimiting("verification");

        app.MapPost("/api/auth/reset-password", async (ResetPasswordRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Password reset is unavailable for temporary demo accounts.", statusCode: 503);
            try
            {
                var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
                var userId = await auth.ResetPasswordAsync(request.Token, request.NewPassword, ct);
                if (userId is null) return Results.BadRequest(new { error = "The password reset token is invalid or expired." });
                var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
                await security.RecordAuditAsync(userId.Value, userId.Value, "password_reset_completed", "Password changed and existing sessions revoked", ct);
                return Results.Ok(new { reset = true });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireRateLimiting("verification");

        app.MapPost("/api/auth/logout", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var token = GetBearerToken(request);
            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                context.RequestServices.GetRequiredService<DemoMembershipAuthStore>().Revoke(token);
                return Results.NoContent();
            }
            var store = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
            await store.RevokeSessionAsync(token, ct);
            return Results.NoContent();
        });

        app.MapPost("/api/auth/request-email-verification", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            VerificationIdentity? identity;
            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                var demoMember = await ResolveMemberAsync(request, context, databaseUrl, ct);
                if (demoMember is null) return Results.Unauthorized();
                identity = new VerificationIdentity(demoMember.UserId, demoMember.Email, demoMember.IdentityVerified);
            }
            else
            {
                identity = await ResolveVerificationIdentityAsync(request, context, databaseUrl, ct);
                if (identity is null) return Results.Unauthorized();
            }
            if (identity.IdentityVerified) return Results.Ok(new { message = "Identity is already verified.", deliveryConfigured = true, deliveryAccepted = true, alreadyVerified = true });
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Email verification is not required for controlled demo accounts.", statusCode: 503);

            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var emailSender = context.RequestServices.GetRequiredService<ResendEmailSender>();
            var created = await security.CreateVerificationTokenAsync(identity.Email, ct);
            var delivered = false;
            if (created is not null)
            {
                await security.RecordAuditAsync(created.Value.UserId, identity.UserId, "email_verification_requested", "Verification token issued", ct);
                if (emailSender.IsConfigured)
                {
                    delivered = await emailSender.SendVerificationAsync(created.Value.Email, created.Value.Token, ct);
                    await security.RecordAuditAsync(created.Value.UserId, identity.UserId, delivered ? "email_verification_sent" : "email_verification_send_failed", delivered ? "Verification email accepted by provider" : "Verification email provider returned a failure", ct);
                }
            }

            return Results.Accepted(value: new
            {
                message = delivered ? "Verification email accepted for delivery." : "Verification request created.",
                deliveryConfigured = emailSender.IsConfigured,
                deliveryAccepted = delivered,
                alreadyVerified = false
            });
        }).RequireRateLimiting("verification");

        app.MapPost("/api/auth/verify-email", async (EmailVerificationConfirmRequest request, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Email verification is not required for controlled demo accounts.", statusCode: 503);
            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var userId = await security.VerifyEmailAsync(request.Token, ct);
            return userId is null ? Results.BadRequest(new { error = "The verification token is invalid or expired." }) : Results.Ok(new { verified = true });
        }).RequireRateLimiting("verification");

        app.MapGet("/api/membership/me", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            return member is null ? Results.Unauthorized() : Results.Ok(member);
        });

        app.MapGet("/api/dashboard", async (HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            var decision = MembershipAuthorization.CanAccessDashboard(member);
            if (!decision.Allowed)
                return Results.Json(new { error = decision.Reason, planCode = member.PlanCode }, statusCode: 403);

            return Results.Ok(new DashboardSummary(member.UserId, member.Email, member.DisplayName, member.PlanCode,
                member.SubscriptionStatus, member.Roles, member.Permissions, true, member.PhoneNumber, member.IdentityVerified));
        });

        app.MapGet("/api/authz/{permission}", async (string permission, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var member = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (member is null) return Results.Unauthorized();
            return Results.Ok(MembershipAuthorization.HasPermission(member, permission));
        });

        app.MapPost("/api/admin/membership/users/{targetUserId:guid}/roles/{roleCode}", async (
            Guid targetUserId, string roleCode, HttpRequest request, HttpContext context, CancellationToken ct) =>
        {
            var actor = await ResolveMemberAsync(request, context, databaseUrl, ct);
            if (actor is null) return Results.Unauthorized();
            if (!actor.Roles.Contains(MembershipRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(databaseUrl)) return Results.Problem("Demo roles are fixed and cannot be modified.", statusCode: 503);

            var security = context.RequestServices.GetRequiredService<PostgresMembershipSecurityStore>();
            var assigned = await security.AssignRoleAsync(targetUserId, roleCode, actor.UserId, $"Platform administrator assigned role '{roleCode}'.", ct);
            return assigned ? Results.Ok(new { assigned = true, targetUserId, roleCode }) : Results.BadRequest(new { error = "Role could not be assigned or was already present." });
        });

        return app;
    }

    private static async Task<VerificationIdentity?> ResolveVerificationIdentityAsync(
        HttpRequest request, HttpContext context, string databaseUrl, CancellationToken ct)
    {
        var token = GetBearerToken(request);
        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return null;

        await using var connection = new Npgsql.NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        const string sql = """
            SELECT email,(email_verified OR phone_verified OR identity_verified_at IS NOT NULL)
            FROM app_users WHERE id=@id AND status IN ('pending','active');
            """;
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new VerificationIdentity(userId.Value, reader.GetString(0), reader.GetBoolean(1))
            : null;
    }

    private sealed record VerificationIdentity(Guid UserId, string Email, bool IdentityVerified);

    private static async Task<AuthenticatedMember?> ResolveMemberAsync(HttpRequest request, HttpContext context, string? databaseUrl, CancellationToken ct)
    {
        var token = GetBearerToken(request);
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return context.RequestServices.GetRequiredService<DemoMembershipAuthStore>().Resolve(token);

        var auth = context.RequestServices.GetRequiredService<PostgresMembershipAuthStore>();
        var userId = await auth.ValidateSessionAsync(token, ct);
        if (userId is null) return null;

        var repository = context.RequestServices.GetRequiredService<PostgresMembershipRepository>();
        var access = await repository.GetAccessAsync(userId.Value, ct);
        if (access is null) return null;

        await using var connection = new Npgsql.NpgsqlConnection(databaseUrl);
        await connection.OpenAsync(ct);
        await using var command = new Npgsql.NpgsqlCommand("SELECT email,display_name,email_verified,phone_number,phone_verified,identity_verified_at FROM app_users WHERE id=@id AND status='active'", connection);
        command.Parameters.AddWithValue("id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var email = reader.GetString(0);
        var displayName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var emailVerified = reader.GetBoolean(2);
        var phoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
        var phoneVerified = reader.GetBoolean(4);
        DateTimeOffset? identityVerifiedAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5);
        return new AuthenticatedMember(userId.Value, email, displayName, emailVerified, access.Roles, access.Permissions, access.PlanCode, access.SubscriptionStatus, access.CurrentPeriodEnd,
            phoneNumber, phoneVerified, identityVerifiedAt);
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }

    private static bool SecretMatches(string expected, string supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
