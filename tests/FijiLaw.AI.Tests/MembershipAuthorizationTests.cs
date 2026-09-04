using FijiLaw.Domain;
using Xunit;

namespace FijiLaw.AI.Tests;

public sealed class MembershipAuthorizationTests
{
    private static AuthenticatedMember Member(bool verified, string planCode = MembershipPlans.PersonalPlus, params string[] permissions) =>
        new(Guid.NewGuid(), "member@example.com", "Member", verified,
            new[] { MembershipRoles.Citizen }, permissions,
            planCode, "active", DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public void VerifiedFreeMemberCanAccessDashboardChatWorkspace()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(true, MembershipPlans.Free));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void UnentitledPaidMemberCannotAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(true));
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void UnverifiedPaidMemberCannotAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(false, MembershipPlans.PersonalPlus, MembershipPermissions.DashboardAccess));
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void VerifiedEntitledMemberCanAccessDashboard()
    {
        var decision = MembershipAuthorization.CanAccessDashboard(Member(true, MembershipPlans.PersonalPlus, MembershipPermissions.DashboardAccess));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void PermissionCheckRejectsMissingPermission()
    {
        var decision = MembershipAuthorization.HasPermission(Member(true, MembershipPlans.PersonalPlus, MembershipPermissions.DashboardAccess), MembershipPermissions.BillingManage);
        Assert.False(decision.Allowed);
    }
}
