using FijiLaw.Domain;

namespace FijiLaw.AI;

public sealed class CuratedFijiLegalSourceRetriever : ILegalSourceRetriever
{
    private sealed record CuratedSource(string Domain, string[] Terms, LegalAuthority Authority);

    private static readonly CuratedSource[] Sources =
    {
        new("Consumer Rights", new[] { "consumer", "refund", "warranty", "merchant", "goods", "shop", "retailer", "product", "misleading" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 75 — Misleading or deceptive conduct", "https://www.laws.gov.fj/Acts/ViewSection/70438", true)),
        new("Consumer Rights", new[] { "consumer", "goods", "quality", "advertised", "representation", "retailer", "product" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 77 — False or misleading representation", "https://www.laws.gov.fj/Acts/ViewSection/70440", true)),
        new("Consumer Rights", new[] { "consumer", "defective", "faulty", "broken", "merchantable", "quality", "refund", "replacement", "goods", "product" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 114 — Action in respect of goods of unmerchantable quality", "https://www.laws.gov.fj/Acts/ViewSection/70493", true)),

        new("Employment", new[] { "employment", "employer", "dismiss", "dismissed", "summary dismissal", "misconduct", "workplace", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 33 — Summary dismissal", "https://www.laws.gov.fj/Acts/ViewSection/82170", true)),
        new("Employment", new[] { "employment", "contract", "fixed term", "expiry", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 40 — Termination of contract by expiry of term or death", "https://www.laws.gov.fj/Acts/ViewSection/82178", true)),
        new("Employment", new[] { "employment", "contract", "termination", "sickness", "accident", "unable", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 41 — Termination of contract in other circumstances", "https://www.laws.gov.fj/Acts/ViewSection/82179", true)),

        new("Domestic Violence", new[] { "domestic violence", "violence", "abuse", "protection order", "restraining order", "threat" },
            new LegalAuthority("Domestic Violence Act 2009", "s 22 — Interim and final domestic violence restraining orders", "https://www.laws.gov.fj/Acts/ViewSection/106845", true)),

        new("Family Law", new[] { "family", "child maintenance", "child support", "maintenance", "parent", "child" },
            new LegalAuthority("Family Law Act 2003", "s 86 — Parents have primary duty to maintain child", "https://www.laws.gov.fj/Acts/ViewSection/106314", true)),
        new("Family Law", new[] { "family", "child maintenance", "child support", "maintenance", "apply", "application" },
            new LegalAuthority("Family Law Act 2003", "s 88 — Who may apply for a child maintenance order", "https://www.laws.gov.fj/Acts/ViewSection/106317", true)),
        new("Family Law", new[] { "family", "spousal maintenance", "spouse", "maintenance", "marriage" },
            new LegalAuthority("Family Law Act 2003", "s 155 — Right of spouse to maintenance", "https://www.laws.gov.fj/Acts/ViewSection/124908", true)),

        new("Criminal Procedure", new[] { "criminal", "arrest", "police", "force", "custody", "detained" },
            new LegalAuthority("Criminal Procedure Act 2009", "s 10 — Procedure to make an arrest", "https://www.laws.gov.fj/Acts/ViewSection/64698", true)),
        new("Criminal Procedure", new[] { "criminal", "arrest", "police", "without warrant", "warrant", "indictable", "detained" },
            new LegalAuthority("Criminal Procedure Act 2009", "s 18 — Arrest by police officers without warrant", "https://www.laws.gov.fj/Acts/ViewSection/64707", true)),

        new("Land & Customary Land", new[] { "itaukei", "mataqali", "native land", "customary land", "lease", "landowner" },
            new LegalAuthority("iTaukei Land Trust Act 1940", "Part 2 — Control of iTaukei Land", "https://www.laws.gov.fj/Acts/DisplayAct/390", true)),
        new("Land & Customary Land", new[] { "title", "land transfer", "certificate of title", "registered land" },
            new LegalAuthority("Land Transfer Act 1971", "Registration and transfer of interests in land", "https://www.laws.gov.fj/Acts/DisplayAct/2612", true)),

        new("Legal Profession", new[] { "lawyer", "legal practitioner", "practising certificate", "fiji law society" },
            new LegalAuthority("Legal Practitioners Act 2009", "Legal practitioners, admission and professional regulation", "https://www.laws.gov.fj/Acts/DisplayAct/2885", true)),

        new("Constitutional Law", new[] { "constitution", "constitutional", "rights", "bill of rights", "freedom", "section 44", "constitutional redress" },
            new LegalAuthority("Constitution of the Republic of Fiji", "s 44 — Enforcement of Chapter 2 rights in the High Court", "https://www.laws.gov.fj/ResourceFile/Get/?fileName=2013+Constitution+of+Fiji+%28English%29.pdf", true)),

        new("Public & Administrative Law", new[] { "judicial review", "administrative decision", "public decision", "public body", "public office", "appointment", "jsc", "judicial services commission", "ultra vires" },
            new LegalAuthority("High Court Rules 1988", "Order 53 — Applications for judicial review", "https://www.laws.gov.fj/Acts/DisplayAct/2929", true)),
        new("Public & Administrative Law", new[] { "judicial review", "leave", "order 53", "administrative decision", "public appointment" },
            new LegalAuthority("High Court Rules 1988", "O 53 r 3 — Application for leave to apply for judicial review", "https://www.laws.gov.fj/Acts/ViewSection/83880", true)),

        new("Public Governance", new[] { "ficac", "commissioner", "judicial services commission", "jsc", "appointment", "anti-corruption commission" },
            new LegalAuthority("Fiji Independent Commission Against Corruption Act 2007", "s 5 — Office of Commissioner; appointment by the President on recommendation of the Judicial Services Commission", "https://laws.gov.fj/Acts/ViewSection/94132", true)),
        new("Public Governance", new[] { "ficac", "commission", "anti-corruption", "commissioner", "establishment" },
            new LegalAuthority("Fiji Independent Commission Against Corruption Act 2007", "s 3 — Establishment of the Commission", "https://laws.gov.fj/Acts/ViewSection/94130", true)),
        new("Public Governance", new[] { "ficac", "prosecution", "charge", "commissioner", "offence" },
            new LegalAuthority("Fiji Independent Commission Against Corruption Act 2007", "s 12B — Power of the Commissioner to institute and conduct prosecutions", "https://laws.gov.fj/Acts/ViewSection/94108", true)),

        new("Agricultural Tenancy", new[] { "agricultural tenancy", "agricultural tenant", "farm lease", "agricultural landlord" },
            new LegalAuthority("Agricultural Landlord and Tenant Act 1966", "Agricultural tenancies and security of tenure", "https://www.laws.gov.fj/Acts/DisplayAct/361", true))
    };

    public Task<IReadOnlyList<LegalAuthority>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<LegalAuthority>>(Array.Empty<LegalAuthority>());

        var text = query.ToLowerInvariant();
        var preferredDomains = DetectStrongDomains(text);

        var ranked = Sources
            .Select(source => new
            {
                Source = source,
                Score = source.Terms.Count(term => text.Contains(term, StringComparison.Ordinal))
            })
            .Where(x => x.Score > 0)
            .Where(x => preferredDomains.Count == 0 || preferredDomains.Contains(x.Source.Domain))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Source.Authority.Title)
            .Select(x => x.Source.Authority)
            .DistinctBy(a => $"{a.Title}|{a.Provision}")
            .Take(6)
            .ToArray();

        return Task.FromResult<IReadOnlyList<LegalAuthority>>(ranked);
    }

    private static HashSet<string> DetectStrongDomains(string text)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var publicLawSignals = new[] { "ficac", "judicial review", "jsc", "judicial services commission", "public appointment", "public office", "administrative decision", "constitutional appointment", "commissioner appointment" };
        if (publicLawSignals.Any(text.Contains))
        {
            domains.Add("Public & Administrative Law");
            domains.Add("Public Governance");
            if (text.Contains("constitution") || text.Contains("constitutional") || text.Contains("section 44"))
                domains.Add("Constitutional Law");
            return domains;
        }

        if (new[] { "consumer", "refund", "warranty", "faulty goods", "retailer" }.Any(text.Contains)) domains.Add("Consumer Rights");
        if (new[] { "employment", "employer", "dismiss", "workplace", "wage" }.Any(text.Contains)) domains.Add("Employment");
        if (new[] { "domestic violence", "protection order", "restraining order" }.Any(text.Contains)) domains.Add("Domestic Violence");
        if (new[] { "family law", "child maintenance", "spousal maintenance", "divorce" }.Any(text.Contains)) domains.Add("Family Law");
        if (new[] { "criminal", "arrest", "bail", "detained" }.Any(text.Contains)) domains.Add("Criminal Procedure");
        if (new[] { "itaukei land", "mataqali", "customary land", "land transfer" }.Any(text.Contains)) domains.Add("Land & Customary Land");

        return domains;
    }
}
