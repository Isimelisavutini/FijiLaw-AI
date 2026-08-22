using FijiLaw.Domain;

namespace FijiLaw.AI;

public sealed class CuratedFijiLegalSourceRetriever : ILegalSourceRetriever
{
    private static readonly (string[] Terms, LegalAuthority Authority)[] Sources =
    {
        (new[] { "consumer", "refund", "warranty", "merchant", "goods", "shop", "retailer", "product", "misleading" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 75 — Misleading or deceptive conduct", "https://www.laws.gov.fj/Acts/ViewSection/70438", true)),
        (new[] { "consumer", "goods", "quality", "advertised", "representation", "retailer", "product" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 77 — False or misleading representation", "https://www.laws.gov.fj/Acts/ViewSection/70440", true)),
        (new[] { "consumer", "defective", "faulty", "broken", "merchantable", "quality", "refund", "replacement", "goods", "product" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "s 114 — Action in respect of goods of unmerchantable quality", "https://www.laws.gov.fj/Acts/ViewSection/70493", true)),

        (new[] { "employment", "employer", "dismiss", "dismissed", "summary dismissal", "misconduct", "workplace", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 33 — Summary dismissal", "https://www.laws.gov.fj/Acts/ViewSection/82170", true)),
        (new[] { "employment", "contract", "fixed term", "expiry", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 40 — Termination of contract by expiry of term or death", "https://www.laws.gov.fj/Acts/ViewSection/82178", true)),
        (new[] { "employment", "contract", "termination", "sickness", "accident", "unable", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "s 41 — Termination of contract in other circumstances", "https://www.laws.gov.fj/Acts/ViewSection/82179", true)),

        (new[] { "domestic violence", "violence", "abuse", "protection order", "restraining order", "threat" },
            new LegalAuthority("Domestic Violence Act 2009", "s 22 — Interim and final domestic violence restraining orders", "https://www.laws.gov.fj/Acts/ViewSection/106845", true)),

        (new[] { "family", "child maintenance", "child support", "maintenance", "parent", "child" },
            new LegalAuthority("Family Law Act 2003", "s 86 — Parents have primary duty to maintain child", "https://www.laws.gov.fj/Acts/ViewSection/106314", true)),
        (new[] { "family", "child maintenance", "child support", "maintenance", "apply", "application" },
            new LegalAuthority("Family Law Act 2003", "s 88 — Who may apply for a child maintenance order", "https://www.laws.gov.fj/Acts/ViewSection/106317", true)),
        (new[] { "family", "spousal maintenance", "spouse", "maintenance", "marriage" },
            new LegalAuthority("Family Law Act 2003", "s 155 — Right of spouse to maintenance", "https://www.laws.gov.fj/Acts/ViewSection/124908", true)),

        (new[] { "criminal", "arrest", "police", "force", "custody", "detained" },
            new LegalAuthority("Criminal Procedure Act 2009", "s 10 — Procedure to make an arrest", "https://www.laws.gov.fj/Acts/ViewSection/64698", true)),
        (new[] { "criminal", "arrest", "police", "without warrant", "warrant", "indictable", "detained" },
            new LegalAuthority("Criminal Procedure Act 2009", "s 18 — Arrest by police officers without warrant", "https://www.laws.gov.fj/Acts/ViewSection/64707", true)),

        (new[] { "itaukei", "mataqali", "native land", "customary land", "lease", "landowner" },
            new LegalAuthority("iTaukei Land Trust Act 1940", "Part 2 — Control of iTaukei Land", "https://www.laws.gov.fj/Acts/DisplayAct/390", true)),
        (new[] { "title", "land transfer", "certificate of title", "registered land" },
            new LegalAuthority("Land Transfer Act 1971", "Registration and transfer of interests in land", "https://www.laws.gov.fj/Acts/DisplayAct/2612", true)),
        (new[] { "lawyer", "legal practitioner", "practising certificate", "fiji law society" },
            new LegalAuthority("Legal Practitioners Act 2009", "Legal practitioners, admission and professional regulation", "https://www.laws.gov.fj/Acts/DisplayAct/2885", true)),
        (new[] { "constitution", "constitutional", "rights", "bill of rights", "freedom" },
            new LegalAuthority("Constitution of the Republic of Fiji", "Chapter 2 — Bill of Rights", "https://www.laws.gov.fj/ResourceFile/Get/?fileName=2013+Constitution+of+Fiji+%28English%29.pdf", true)),
        (new[] { "agricultural tenancy", "agricultural tenant", "farm lease", "agricultural landlord" },
            new LegalAuthority("Agricultural Landlord and Tenant Act 1966", "Agricultural tenancies and security of tenure", "https://www.laws.gov.fj/Acts/DisplayAct/361", true))
    };

    public Task<IReadOnlyList<LegalAuthority>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<LegalAuthority>>(Array.Empty<LegalAuthority>());

        var text = query.ToLowerInvariant();
        var results = Sources
            .Where(source => source.Terms.Any(text.Contains))
            .Select(source => source.Authority)
            .DistinctBy(a => $"{a.Title}|{a.Provision}")
            .Take(8)
            .ToArray();

        return Task.FromResult<IReadOnlyList<LegalAuthority>>(results);
    }
}
