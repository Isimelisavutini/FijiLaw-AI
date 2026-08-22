using System.Text.RegularExpressions;
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
    private const string Disclaimer = "FijiLaw AI provides legal information, legal triage and AI-assisted legal research. It does not independently provide legal representation. Authorities, limitation periods and procedural requirements should be verified against current Fiji law before legal action is taken.";

    public async Task<LegalTriageResult> TriageAsync(LegalTriageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Situation)) throw new ArgumentException("Situation is required.", nameof(request));
        var text = request.Situation.Trim();
        var lower = text.ToLowerInvariant();
        var domains = ClassifyDomains(lower);
        var issue = PrimaryIssue(domains);
        var risk = Risk(lower);
        var verified = (await sources.SearchAsync(text, ct)).Where(a => a.Verified).ToArray();
        var missing = MissingInfo(domains, lower);
        var fallbackGuidance = verified.Length == 0
            ? "No verified Fiji legal authority is currently connected to this request. FijiLaw AI will not invent legislation, sections, cases, deadlines or procedural rules."
            : GuidanceFor(domains);
        var modelAuthorities = verified.Select(a => $"{a.Title}{(string.IsNullOrWhiteSpace(a.Provision) ? "" : $" — {a.Provision}")}{(string.IsNullOrWhiteSpace(a.SourceUrl) ? "" : $" — {a.SourceUrl}")}").ToArray();
        var modelGuidance = await _model.GenerateGuidanceAsync(new LegalModelRequest(text, issue, risk.ToString(), modelAuthorities, missing), ct);
        var guidance = string.IsNullOrWhiteSpace(modelGuidance) ? fallbackGuidance : modelGuidance;
        var correlationId = Guid.NewGuid().ToString("N");
        var nextSteps = NextSteps(domains, risk);
        var report = BuildAdvancedReport(text, request.Location, domains, risk, verified, missing, nextSteps, correlationId);

        return new LegalTriageResult(issue, new[] { text }, missing, verified, guidance, nextSteps, risk,
            risk is LegalRiskLevel.High or LegalRiskLevel.Restricted, Disclaimer, correlationId, domains, report);
    }

    private static AdvancedLegalTriageReport BuildAdvancedReport(string text, string? location, IReadOnlyList<string> domains,
        LegalRiskLevel risk, IReadOnlyList<LegalAuthority> authorities, IReadOnlyList<string> missing,
        IReadOnlyList<string> nextSteps, string correlationId)
    {
        var primary = PrimaryIssue(domains);
        var secondary = domains.Where(x => !primary.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray();
        var authorityRows = authorities.Select(a => new AuthorityAnalysis(
            CategoryFor(a),
            $"{a.Title}{(string.IsNullOrWhiteSpace(a.Provision) ? "" : $", {a.Provision}")}",
            "Retrieved as a verified authority relevant to the detected legal domains. The precise legal effect must be applied to the confirmed facts.",
            a.SourceUrl,
            a.Verified)).ToArray();

        var vulnerabilities = domains.Select(domain => new VulnerabilityItem(
            CauseFor(domain),
            authorities.Count == 0 ? "Legal threshold cannot be stated until a directly relevant authority is verified." : ThresholdFor(domain),
            "Assessment depends on the user's stated facts and the outstanding information identified in this report.",
            risk.ToString().ToUpperInvariant())).ToArray();

        var dateMatches = Regex.Matches(text, @"\b(?:[0-3]?\d[/-][01]?\d[/-](?:19|20)\d{2}|(?:19|20)\d{2}-[01]\d-[0-3]\d)\b")
            .Select(m => m.Value).Distinct().ToArray();
        var proceduralRule = FindVerifiedProceduralRule(authorities);
        var roadmap = new ProceduralRoadmap(
            dateMatches.FirstOrDefault(),
            dateMatches.Skip(1).FirstOrDefault(),
            proceduralRule is null ? "NOT CALCULATED — no verified limitation rule was retrieved. Do not infer a deadline from model memory." : "A potentially relevant procedural authority was retrieved; the operative deadline still requires fact-specific legal verification.",
            proceduralRule,
            null,
            null,
            nextSteps);

        var gaps = new EvidenceGapAnalysis(
            new[] { text },
            Array.Empty<string>(),
            missing.Where(x => x.Contains("Copies", StringComparison.OrdinalIgnoreCase) || x.Contains("agreement", StringComparison.OrdinalIgnoreCase) || x.Contains("documents", StringComparison.OrdinalIgnoreCase)).ToArray(),
            missing.Where(x => x.Contains("date", StringComparison.OrdinalIgnoreCase)).ToArray(),
            missing);

        var verification = new VerificationStatement(
            authorities.Count >= 3 ? "Higher — multiple verified authorities retrieved; factual application still requires review." : authorities.Count > 0 ? "Moderate — at least one verified authority retrieved; corpus coverage may be incomplete." : "Low — no verified authority retrieved.",
            authorities.Count,
            authorities.Count == 0 ? new[] { "Applicable Fiji legislation, procedural rules and case law" } : Array.Empty<string>(),
            missing,
            DateTimeOffset.UtcNow.ToString("O"));

        return new AdvancedLegalTriageReport(
            "FijiLaw AI — Advanced Legal Triage Report",
            $"FJ-{DateTimeOffset.UtcNow:yyyy}-{DateTimeOffset.UtcNow:MMdd}-{CaseTag(domains)}",
            risk is LegalRiskLevel.High or LegalRiskLevel.Restricted ? "HIGH" : risk == LegalRiskLevel.Medium ? "MEDIUM" : "LOW",
            string.IsNullOrWhiteSpace(location) ? "Fiji — registry/division to be confirmed from the facts" : $"Fiji — {location}; competent court/registry to be confirmed",
            primary,
            secondary,
            DoctrinalTags(domains),
            authorityRows,
            vulnerabilities,
            roadmap,
            gaps,
            nextSteps,
            risk is LegalRiskLevel.High or LegalRiskLevel.Restricted ? "Prompt review by a qualified Fiji legal practitioner is recommended." : "Human legal review is recommended before relying on contested legal conclusions, deadlines or court procedure.",
            verification,
            Disclaimer);
    }

    private static string? FindVerifiedProceduralRule(IReadOnlyList<LegalAuthority> authorities) =>
        authorities.FirstOrDefault(a => a.Title.Contains("High Court Rules", StringComparison.OrdinalIgnoreCase) || (a.Provision?.Contains("Order", StringComparison.OrdinalIgnoreCase) ?? false)) is { } a
            ? $"{a.Title}{(string.IsNullOrWhiteSpace(a.Provision) ? "" : $", {a.Provision}")}" : null;

    private static string CategoryFor(LegalAuthority a)
    {
        if (a.Title.Contains("Constitution", StringComparison.OrdinalIgnoreCase)) return "Supreme Law";
        if (a.Title.Contains("Rules", StringComparison.OrdinalIgnoreCase)) return "Procedural Rules";
        if (a.Title.Contains("Act", StringComparison.OrdinalIgnoreCase)) return "Primary Statute";
        return "Verified Authority";
    }

    private static string CauseFor(string domain) => domain switch
    {
        "Public & Administrative Law" => "Legality of public decision / judicial review grounds",
        "Constitutional Law" => "Constitutional rights or constitutional legality",
        "Employment" => "Employment rights / termination dispute",
        "Criminal Procedure" => "Criminal procedural rights",
        "Land & Customary Land" => "Land or customary-land rights dispute",
        "Tenancy" => "Tenancy rights dispute",
        "Family Law" => "Family-law relief",
        "Domestic Violence" => "Protective / domestic-violence relief",
        "Consumer Rights" => "Consumer-law remedy",
        _ => "Potential legal claim or remedy"
    };

    private static string ThresholdFor(string domain) => domain switch
    {
        "Public & Administrative Law" => "Confirm the decision-maker's legal power, mandatory procedure, standing, reviewable decision and any applicable procedural requirements from verified authorities.",
        "Constitutional Law" => "Identify the specific constitutional provision/right, alleged contravention, responsible actor and available verified redress pathway.",
        _ => "Apply the elements of the retrieved verified authority to the confirmed facts; unresolved elements remain information gaps."
    };

    private static IReadOnlyList<string> DoctrinalTags(IReadOnlyList<string> domains)
    {
        var tags = new List<string>();
        if (domains.Contains("Public & Administrative Law")) tags.AddRange(new[] { "Judicial Review", "Lawful Authority", "Procedural Fairness" });
        if (domains.Contains("Constitutional Law")) tags.Add("Constitutional Redress");
        if (domains.Contains("Public Governance")) tags.Add("Public Appointment / Governance");
        if (tags.Count == 0) tags.AddRange(domains.Take(4));
        return tags.Distinct().Take(6).ToArray();
    }

    private static string CaseTag(IReadOnlyList<string> domains) => domains.FirstOrDefault() switch
    {
        "Constitutional Law" => "CONST",
        "Public & Administrative Law" => "ADMIN",
        "Employment" => "EMP",
        "Criminal Procedure" => "CRIM",
        "Land & Customary Land" => "LAND",
        "Family Law" => "FAM",
        "Domestic Violence" => "DV",
        "Consumer Rights" => "CONS",
        "Tenancy" => "TEN",
        _ => "GEN"
    };

    private static IReadOnlyList<string> ClassifyDomains(string text)
    {
        var domains = new List<string>();
        if (Contains(text, "constitution", "constitutional", "bill of rights", "section 44", "fundamental right")) domains.Add("Constitutional Law");
        if (Contains(text, "judicial review", "administrative decision", "public body", "public office", "appointment", "jsc", "judicial services commission", "ficac", "commissioner", "government decision", "ultra vires")) domains.Add("Public & Administrative Law");
        if (Contains(text, "ficac", "public governance", "public office", "commissioner", "integrity commission", "appointment process")) domains.Add("Public Governance");
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
        if (domains.Contains("Public & Administrative Law") && domains.Contains("Constitutional Law")) return "Public & Administrative Law (Judicial Review) / Constitutional Law";
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
            var items = new List<string> { "The exact public decision, appointment, charge, direction or refusal being challenged", "Date the decision or appointment instrument was made and date you became aware of it", "The decision-maker or public body involved and the legal power said to authorise the decision", "Copies of appointment instruments, charge notices, official correspondence, minutes or Gazette material", "The outcome or remedy sought" };
            if (text.Contains("ficac")) items.Add("Any FICAC charge notice, summons, warrant, correspondence or prosecution document relevant to the dispute");
            if (text.Contains("jsc") || text.Contains("appointment")) items.Add("The JSC recommendation/appointment instrument and relevant consultation/recommendation dates, if known");
            return items;
        }
        if (domains.Contains("Employment")) return new[] { "Employment dates and role", "Contract or appointment terms", "Relevant letters/messages", "Key event dates" };
        if (domains.Contains("Tenancy")) return new[] { "Tenancy agreement", "Rent/payment history", "Notices received", "Property location and key dates" };
        if (domains.Contains("Criminal Procedure")) return new[] { "Current custody/charge status", "Court or police documents", "Important dates", "Whether a lawyer is already involved" };
        if (domains.Contains("Land & Customary Land")) return new[] { "Land type and location", "Relevant lease/title documents", "Parties involved", "Decision or dispute being challenged" };
        if (domains.Contains("Family Law")) return new[] { "Relationship and dependent details relevant to the issue", "Existing orders/agreements", "Important dates", "Immediate safety concerns" };
        return new[] { "What outcome you want", "Important dates/deadlines", "Documents or evidence available", "Other parties involved" };
    }

    private static string GuidanceFor(IReadOnlyList<string> domains) => domains.Contains("Public & Administrative Law")
        ? "This appears to involve the legality of a public decision or appointment. The retrieved verified authorities should be used to identify the source of power, mandatory procedure and any available review/redress pathway."
        : "Relevant verified sources were retrieved. Review the cited authorities and obtain human legal review where indicated.";

    private static IReadOnlyList<string> NextSteps(IReadOnlyList<string> domains, LegalRiskLevel risk)
    {
        var steps = new List<string>();
        if (risk is LegalRiskLevel.High or LegalRiskLevel.Restricted) steps.Add("Seek prompt review by a qualified legal practitioner or appropriate authority; do not rely on the AI alone.");
        steps.Add("Preserve relevant documents, messages, dates and evidence.");
        steps.Add("Provide the missing information so the issue can be assessed more accurately.");
        if (domains.Contains("Public & Administrative Law"))
        {
            steps.Add("Identify and verify the exact statutory or constitutional power under which the public decision or appointment was made.");
            steps.Add("Retrieve and verify the applicable High Court procedural rules before calculating or relying on any judicial-review deadline.");
            if (domains.Contains("Constitutional Law")) steps.Add("Retrieve and verify the applicable constitutional redress provision before relying on that pathway.");
            return steps;
        }
        steps.Add("Retrieve and verify the applicable Fiji legislation or case law before acting on legal conclusions.");
        return steps;
    }

    private static bool Contains(string text, params string[] terms) => terms.Any(text.Contains);
}
