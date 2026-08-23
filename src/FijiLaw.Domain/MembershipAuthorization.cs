namespace FijiLaw.Domain;

public static class MembershipAuthorization
{
    public static AuthorizationDecision CanAccessDashboard(AuthenticatedMember member)
    {
        if (!member.IdentityVerified)
            return AuthorizationDecision.Deny("Verified identity is required.");

        if (!member.Permissions.Contains(MembershipPermissions.DashboardAccess, StringComparer.OrdinalIgnoreCase))
            return AuthorizationDecision.Deny("Dashboard.Access entitlement is required.");

        return AuthorizationDecision.Allow();
    }

    public static AuthorizationDecision HasPermission(AuthenticatedMember member, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return AuthorizationDecision.Deny("A permission code is required.");

        if (!member.IdentityVerified)
            return AuthorizationDecision.Deny("Verified identity is required.");

        return member.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny("Permission not granted by active role or subscription.");
    }
}
