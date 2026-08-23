namespace FijiLaw.Domain;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null, string? RequestedPlanCode = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record EmailVerificationRequest(string Email);
public sealed record EmailVerificationConfirmRequest(string Token);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Trusted identity hand-off from the FijiLaw web server after an upstream
/// provider (Google, Apple, email OTP, or phone OTP) has verified the user.
/// This request must only be accepted through the server-to-server auth bridge.
/// </summary>
public sealed record ExternalIdentitySessionRequest(
    string IdentityProvider,
    string IdentitySubject,
    string? Email,
    string? PhoneNumber,
    bool EmailVerified,
    bool PhoneVerified,
    string? DisplayName = null,
    string? RequestedPlanCode = null);

public sealed record AuthSessionResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    string? DisplayName,
    string? PhoneNumber = null,
    bool IdentityVerified = false);

public sealed record AuthenticatedMember(
    Guid UserId,
    string Email,
    string? DisplayName,
    bool EmailVerified,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string PlanCode,
    string SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEnd,
    string? PhoneNumber = null,
    bool PhoneVerified = false,
    DateTimeOffset? IdentityVerifiedAt = null)
{
    public bool IdentityVerified => EmailVerified || PhoneVerified || IdentityVerifiedAt is not null;
}

public sealed record DashboardSummary(
    Guid UserId,
    string Email,
    string? DisplayName,
    string PlanCode,
    string SubscriptionStatus,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool DashboardAccess,
    string? PhoneNumber = null,
    bool IdentityVerified = false);

public sealed record AuthorizationDecision(bool Allowed, string? Reason = null)
{
    public static AuthorizationDecision Allow() => new(true);
    public static AuthorizationDecision Deny(string reason) => new(false, reason);
}
