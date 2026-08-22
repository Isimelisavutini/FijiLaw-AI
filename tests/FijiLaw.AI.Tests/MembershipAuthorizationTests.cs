using FijiLaw.Domain;
using Xunit;

namespace FijiLaw.AI.Tests;

public sealed class MembershipAuthorizationTests
{
    private static AuthenticatedMember Member(bool verified, params string[] permissions) =>
        new(Guid.NewGuid(), "member@example.com", "Member", verified,
            new[] { MembershipRoles.Citizen }, permissions,
            MembershipPlans.PersonalPlus, "active", DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public void FreeOrUnentitledMemberCannotAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(true));
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void UnverifiedPaidMemberCannotAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(false, MembershipPermissions.DashboardAccess));
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void VerifiedEntitledMemberCanAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(true, MembershipPermissions.DashboardAccess));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void PermissionCheckRejectsMissingPermission()
    {
        var decision = MembershipAuthorization.HasPermission(Member(true, MembershipPermissions.DashboardAccess), MembershipPermissions.BillingManage);
        Assert.False(decision.Allowed);
    }
}
