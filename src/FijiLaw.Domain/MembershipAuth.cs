namespace FijiLaw.Domain;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthSessionResult(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, string Email, string? DisplayName);
public sealed record AuthenticatedMember(Guid UserId, string Email, string? DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, string PlanCode, string SubscriptionStatus, DateTimeOffset? CurrentPeriodEnd);
public sealed record DashboardSummary(Guid UserId, string Email, string? DisplayName, string PlanCode, string SubscriptionStatus, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool DashboardAccess);
