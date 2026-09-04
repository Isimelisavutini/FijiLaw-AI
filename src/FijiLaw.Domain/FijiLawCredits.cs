namespace FijiLaw.Domain;

public sealed record CreditPackage(string Code, string Name, int Credits, decimal PriceFjd);
public sealed record AiCreditPrice(string ServiceCode, string Name, int Credits, bool Implemented);
public sealed record CreditWalletSnapshot(Guid UserId, int Balance, long LifetimePurchased, long LifetimeGranted, long LifetimeUsed, string? LastAllowanceKey);
public sealed record CreditReservation(Guid TransactionId, Guid UserId, int Credits, string ServiceCode, string CorrelationId);
public sealed record CreditTransactionSummary(Guid Id, string Type, string Status, int Amount, int BalanceBefore, int BalanceAfter, string? ServiceCode, string? CorrelationId, string? ProviderReference, DateTimeOffset CreatedAt);

public interface ICreditWalletStore
{
    Task<CreditWalletSnapshot> GetWalletAsync(Guid userId, string planCode, CancellationToken ct = default);
    Task<CreditReservation?> ReserveAsync(Guid userId, string planCode, int credits, string serviceCode, string correlationId, CancellationToken ct = default);
    Task CompleteAsync(CreditReservation reservation, CancellationToken ct = default);
    Task RefundAsync(CreditReservation reservation, string reason, CancellationToken ct = default);
    Task<CreditWalletSnapshot> GrantAsync(Guid userId, string planCode, int credits, string reason, bool purchased = false, string? providerReference = null, CancellationToken ct = default);
    Task<IReadOnlyList<CreditTransactionSummary>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default);
}

public static class FijiLawCreditCatalog
{
    public const string AdvancedTriage = "advanced_legal_triage";
    public const string DocumentAnalysis = "document_analysis";\n    public const string DashboardLegalChat = "dashboard_legal_chat";

    public static readonly IReadOnlyList<CreditPackage> Packages = new[]
    {
        new CreditPackage("starter", "Starter", 50, 10m),
        new CreditPackage("standard", "Standard", 120, 20m),
        new CreditPackage("plus", "Plus", 300, 45m),
        new CreditPackage("professional", "Professional", 750, 100m),
        new CreditPackage("firm", "Firm", 2000, 250m)
    };

    public static readonly IReadOnlyList<AiCreditPrice> Services = new[]
    {
        new AiCreditPrice(AdvancedTriage, "Advanced Legal Triage Report", 10, true),
        new AiCreditPrice(DocumentAnalysis, "Document analysis", 15, true),\n        new AiCreditPrice(DashboardLegalChat, "Dashboard legal chat", 3, true),
        new AiCreditPrice("follow_up_analysis", "Follow-up analysis", 3, false),
        new AiCreditPrice("detailed_legal_research", "Detailed legal research", 20, false),
        new AiCreditPrice("compare_authorities", "Compare verified authorities", 15, false),
        new AiCreditPrice("lawyer_case_preparation", "Lawyer case-preparation report", 25, false),
        new AiCreditPrice("large_bundle_analysis", "Large bundle analysis", 40, false)
    };

    public static int PriceFor(string serviceCode) => Services.First(x => string.Equals(x.ServiceCode, serviceCode, StringComparison.OrdinalIgnoreCase)).Credits;

    public static int IncludedCredits(string planCode) => planCode switch
    {
        MembershipPlans.Free => 10,
        MembershipPlans.PersonalPlus => 100,
        MembershipPlans.LawyerProfessional => 700,
        MembershipPlans.FirmStarter => 1500,
        MembershipPlans.FirmProfessional => 3500,
        MembershipPlans.FirmPremium => 7500,
        MembershipPlans.Institutional => 5000,
        _ => 0
    };

    public static string AllowanceKey(string planCode, DateTimeOffset now)
        => planCode == MembershipPlans.Free ? "free:intro" : $"{planCode}:{now:yyyy-MM}";
}
