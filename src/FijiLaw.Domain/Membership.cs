namespace FijiLaw.Domain;

public static class MembershipRoles
{
    public const string Citizen = "citizen";
    public const string Lawyer = "lawyer";
    public const string FirmStaff = "firm_staff";
    public const string FirmAdmin = "firm_admin";
    public const string Institutional = "institutional";
    public const string PlatformAdmin = "platform_admin";
}

public static class MembershipPlans
{
    public const string Free = "free";
    public const string PersonalPlus = "personal_plus";
    public const string LawyerProfessional = "lawyer_professional";
    public const string FirmStarter = "firm_starter";
    public const string FirmProfessional = "firm_professional";
    public const string FirmPremium = "firm_premium";
    public const string Institutional = "institutional";
}

public static class MembershipPermissions
{
    public const string DashboardAccess = "Dashboard.Access";
    public const string CasesCreate = "Cases.Create";
    public const string CasesViewOwn = "Cases.ViewOwn";
    public const string CasesManage = "Cases.Manage";
    public const string DocumentsAnalyse = "Documents.Analyse";
    public const string DocumentsStore = "Documents.Store";
    public const string ReferralsRequest = "Referrals.Request";
    public const string ReferralsManage = "Referrals.Manage";
    public const string LeadsView = "Leads.View";
    public const string LeadsManage = "Leads.Manage";
    public const string LawyerProfileManage = "LawyerProfile.Manage";
    public const string FirmManage = "Firm.Manage";
    public const string FirmUsersManage = "FirmUsers.Manage";
    public const string AnalyticsView = "Analytics.View";
    public const string BillingView = "Billing.View";
    public const string BillingManage = "Billing.Manage";
    public const string DirectoryPriorityPlacement = "Directory.PriorityPlacement";
}

public sealed record MembershipPlanSummary(
    string Code,
    string Name,
    string Audience,
    decimal? MonthlyPriceFjd,
    decimal? AnnualPriceFjd,
    bool IsPaid,
    IReadOnlyList<string> Entitlements);

public sealed record MembershipAccessSnapshot(
    Guid UserId,
    string PlanCode,
    string SubscriptionStatus,
    DateTimeOffset? CurrentPeriodEnd,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    public bool DashboardEnabled => Has(MembershipPermissions.DashboardAccess);
}

public sealed record UsageEntry(
    Guid? UserId,
    Guid? OrganisationId,
    Guid? SubscriptionId,
    string UsageType,
    decimal Quantity,
    string Unit,
    decimal? EstimatedCostFjd,
    string? CorrelationId);
