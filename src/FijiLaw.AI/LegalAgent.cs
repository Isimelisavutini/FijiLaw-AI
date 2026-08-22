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
        var domains = ClassifyDomains(lower);
        var issue = PrimaryIssue(domains);
        var risk = Risk(lower);
        var authorities = await sources.SearchAsync(text, ct);
        var verified = authorities.Where(a => a.Verified).ToArray();
        var missing = MissingInfo(domains, lower);

        var fallbackGuidance = verified.Length == 0
            ? "I can help organize the issue and next steps, but no verified Fiji legal authority is currently connected to this request. I will not invent legislation, sections, or cases."
            : GuidanceFor(domains, verified);

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
            NextSteps(domains, risk, lower),
            risk,
            risk is LegalRiskLevel.High or LegalRiskLevel.Restricted,
            Disclaimer,
            Guid.NewGuid().ToString("N"),
            domains);
    }

    private static IReadOnlyList<string> ClassifyDomains(string text)
    {
        var domains = new List<string>();

        if (Contains(text, "constitution", "constitutional", "bill of rights", "section 44", "fundamental right"))
            domains.Add("Constitutional Law");

        if (Contains(text, "judicial review", "administrative decision", "public body", "public office", "appointment", "jsc", "judicial services commission", "ficac", "commissioner", "government decision", "ultra vires"))
            domains.Add("Public & Administrative Law");

        if (Contains(text, "ficac", "public governance", "public office", "commissioner", "integrity commission", "appointment process"))
            domains.Add("Public Governance");

        if (Contains(text, "arrest", "police", "charge", "criminal", "bail")) domains.Add("Criminal Procedure");
        if (Contains(text, "land", "lease", "mataqali", "itaukei")) domains.Add("Land & Customary Land");
        if (Contains(text, "dismiss", "employer", "salary", "wage", "workplace")) domains.Add("Employment");
        if (Contains(text, "rent", "tenant", "landlord", "evict")) domains.Add("Tenancy");
        if (Contains(text, "divorce", "custody", "maintenance", "marriage")) domains.Add("Family Law");
        if (Contains(text, "violence", "abuse", "threat", "protection order")) domains.Add("Domestic Violence");
        if (Contains(text, "refund", "consumer", "warranty", "merchant", "faulty goods")) domains.Add("Consumer Rights");

        if (domains.Count == 0) domains.Add("General Legal Issue");
        return domains.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string PrimaryIssue(IReadOnlyList<string> domains)
    {
        if (domains.Contains("Public & Administrative Law") && domains.Contains("Constitutional Law"))
            return "Constitutional / Public & Administrative Law";
        return domains[0];
    }

    private static LegalRiskLevel Risk(string text)
    {
        if (Contains(text, "court tomorrow", "hearing today", "arrest", "detained", "violence", "abuse", "threat", "suicide", "deport")) return LegalRiskLevel.High;
        if (Contains(text, "court", "criminal", "custody", "evict", "dismiss", "land dispute", "judicial review", "constitutional redress")) return LegalRiskLevel.Medium;
        return LegalRiskLevel.Low;
    }

    private static IReadOnlyList<string> MissingInfo(IReadOnlyList<string> domains, string text)
    {
        if (domains.Contains("Public & Administrative Law"))
        {
            var items = new List<string>
            {
                "The exact public decision, appointment, charge, direction or refusal being challenged",
                "Date the decision or appointment instrument was made and date you became aware of it",
                "The decision-maker or public body involved and the legal power said to authorise the decision",
                "Copies of appointment instruments, charge notices, official correspondence, minutes or Gazette material",
                "The outcome or remedy sought, including whether you want the decision quashed, restrained, reconsidered or constitutional redress"
            };
            if (text.Contains("ficac")) items.Add("Any FICAC charge notice, summons, warrant, correspondence or prosecution document relevant to the dispute");
            if (text.Contains("jsc") || text.Contains("appointment")) items.Add("The JSC recommendation/appointment instrument and the dates of consultation or recommendation, if known");
            return items;
        }

        if (domains.Contains("Employment")) return new[] { "Employment dates and role", "Contract or appointment terms", "Relevant letters/messages", "Key event dates" };
        if (domains.Contains("Tenancy")) return new[] { "Tenancy agreement", "Rent/payment history", "Notices received", "Property location and key dates" };
        if (domains.Contains("Criminal Procedure")) return new[] { "Current custody/charge status", "Court or police documents", "Important dates", "Whether a lawyer is already involved" };
        if (domains.Contains("Land & Customary Land")) return new[] { "Land type and location", "Relevant lease/title documents", "Parties involved", "Decision or dispute being challenged" };
        if (domains.Contains("Family Law")) return new[] { "Relationship and dependent details relevant to the issue", "Existing orders/agreements", "Important dates", "Immediate safety concerns" };
        return new[] { "What outcome you want", "Important dates/deadlines", "Documents or evidence available", "Other parties involved" };
    }

    private static string GuidanceFor(IReadOnlyList<string> domains, IReadOnlyList<LegalAuthority> authorities)
    {
        if (domains.Contains("Public & Administrative Law"))
            return "This appears to involve the legality of a public decision or appointment. The retrieved authorities should be reviewed to identify the source of the decision-maker's power, any mandatory appointment procedure, and whether judicial review or constitutional redress may be available. FijiLaw AI does not determine that a public decision is unlawful without examining the relevant instrument, procedure and evidence.";

        return "Relevant verified sources were retrieved. Review the cited authorities and obtain human legal review where indicated.";
    }

    private static IReadOnlyList<string> NextSteps(IReadOnlyList<string> domains, LegalRiskLevel risk, string text)
    {
        var steps = new List<string>();
        if (risk is LegalRiskLevel.High or LegalRiskLevel.Restricted)
            steps.Add("Seek prompt review by a qualified legal practitioner or appropriate authority; do not rely on the AI alone.");

        steps.Add("Preserve relevant documents, messages, dates and evidence.");
        steps.Add("Provide the missing information so the issue can be assessed more accurately.");

        if (domains.Contains("Public & Administrative Law"))
        {
            steps.Add("Identify the exact statutory or constitutional power under which the public decision or appointment was made.");
            steps.Add("Consider whether the High Court Rules 1988, Order 53 judicial review procedure may be relevant; legal advice should be obtained promptly because procedural requirements and delay can matter.");
            if (domains.Contains("Constitutional Law"))
                steps.Add("If a Chapter 2 constitutional right is said to be contravened, consider whether section 44 of the Constitution may provide a High Court redress pathway.");
            return steps;
        }

        steps.Add("Retrieve and verify the applicable Fiji legislation or case law before acting on legal conclusions.");
        return steps;
    }

    private static bool Contains(string text, params string[] terms) => terms.Any(text.Contains);
}
