using FijiLaw.Domain;

namespace FijiLaw.AI;

public interface ILegalSourceRetriever
{
    Task<IReadOnlyList<LegalAuthority>> SearchAsync(string query, CancellationToken ct = default);
}

public interface ILegalAgent
{
    Task<LegalTriageResult> TriageAsync(LegalTriageRequest request, CancellationToken ct = default);
}

public sealed class EmptyLegalSourceRetriever : ILegalSourceRetriever
{
    public Task<IReadOnlyList<LegalAuthority>> SearchAsync(string query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LegalAuthority>>(Array.Empty<LegalAuthority>());
}

public sealed class LegalAgent(ILegalSourceRetriever sources, ILanguageModelProvider? model = null) : ILegalAgent
{
    private readonly ILanguageModelProvider _model = model ?? new DisabledLanguageModelProvider();
    private const string Disclaimer = "FijiLaw AI provides legal information and triage, not autonomous legal representation. Important or time-sensitive matters should be reviewed by a qualified Fiji legal practitioner.";

    public async Task<LegalTriageResult> TriageAsync(LegalTriageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Situation))
            throw new ArgumentException("Situation is required.", nameof(request));

        var text = request.Situation.Trim();
        var lower = text.ToLowerInvariant();
        var issue = Classify(lower);
        var risk = Risk(lower);
        var authorities = await sources.SearchAsync(text, ct);
        var verified = authorities.Where(a => a.Verified).ToArray();
        var missing = MissingInfo(issue);

        var fallbackGuidance = verified.Length == 0
            ? "I can help organize the issue and next steps, but no verified Fiji legal authority is currently connected to this request. I will not invent legislation, sections, or cases."
            : "Relevant verified sources were retrieved. Review the cited authorities and obtain human legal review where indicated.";

        var modelAuthorities = verified
            .Select(a => $"{a.Title}{(string.IsNullOrWhiteSpace(a.Provision) ? "" : $" — {a.Provision}")}{(string.IsNullOrWhiteSpace(a.SourceUrl) ? "" : $" — {a.SourceUrl}")}")
            .ToArray();

        var modelGuidance = await _model.GenerateGuidanceAsync(
            new LegalModelRequest(text, issue, risk.ToString(), modelAuthorities, missing), ct);

        var guidance = string.IsNullOrWhiteSpace(modelGuidance) ? fallbackGuidance : modelGuidance;

        return new LegalTriageResult(
            issue,
            new[] { text },
            missing,
            verified,
            guidance,
            NextSteps(issue, risk),
            risk,
            risk is LegalRiskLevel.High or LegalRiskLevel.Restricted,
            Disclaimer,
            Guid.NewGuid().ToString("N"));
    }

    private static string Classify(string text)
    {
        if (Contains(text, "arrest", "police", "charge", "criminal", "bail")) return "Criminal procedure";
        if (Contains(text, "land", "lease", "mataqali", "itaukei")) return "Land and customary land";
        if (Contains(text, "dismiss", "employer", "salary", "wage", "workplace")) return "Employment";
        if (Contains(text, "rent", "tenant", "landlord", "evict")) return "Tenancy";
        if (Contains(text, "divorce", "custody", "maintenance", "marriage")) return "Family law";
        if (Contains(text, "violence", "abuse", "threat", "protection order")) return "Personal safety / domestic violence";
        if (Contains(text, "refund", "consumer", "warranty", "merchant")) return "Consumer rights";
        return "General legal issue";
    }

    private static LegalRiskLevel Risk(string text)
    {
        if (Contains(text, "court tomorrow", "hearing today", "arrest", "detained", "violence", "abuse", "threat", "suicide", "deport")) return LegalRiskLevel.High;
        if (Contains(text, "court", "criminal", "custody", "evict", "dismiss", "land dispute")) return LegalRiskLevel.Medium;
        return LegalRiskLevel.Low;
    }

    private static IReadOnlyList<string> MissingInfo(string issue) => issue switch
    {
        "Employment" => new[] { "Employment dates and role", "Contract or appointment terms", "Relevant letters/messages", "Key event dates" },
        "Tenancy" => new[] { "Tenancy agreement", "Rent/payment history", "Notices received", "Property location and key dates" },
        "Criminal procedure" => new[] { "Current custody/charge status", "Court or police documents", "Important dates", "Whether a lawyer is already involved" },
        "Land and customary land" => new[] { "Land type and location", "Relevant lease/title documents", "Parties involved", "Decision or dispute being challenged" },
        "Family law" => new[] { "Relationship and dependent details relevant to the issue", "Existing orders/agreements", "Important dates", "Immediate safety concerns" },
        _ => new[] { "What outcome you want", "Important dates/deadlines", "Documents or evidence available", "Other parties involved" }
    };

    private static IReadOnlyList<string> NextSteps(string issue, LegalRiskLevel risk)
    {
        var steps = new List<string> { "Preserve relevant documents, messages, dates and evidence.", "Provide the missing information so the issue can be assessed more accurately." };
        if (risk is LegalRiskLevel.High or LegalRiskLevel.Restricted) steps.Insert(0, "Seek prompt review by a qualified legal practitioner or appropriate authority; do not rely on the AI alone.");
        else steps.Add("Retrieve and verify the applicable Fiji legislation or case law before acting on legal conclusions.");
        return steps;
    }

    private static bool Contains(string text, params string[] terms) => terms.Any(text.Contains);
}
