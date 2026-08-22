using FijiLaw.Domain;

namespace FijiLaw.AI;

public sealed class CuratedFijiLegalSourceRetriever : ILegalSourceRetriever
{
    private static readonly (string[] Terms, LegalAuthority Authority)[] Sources =
    {
        (new[] { "consumer", "refund", "warranty", "merchant", "goods", "shop", "retailer", "product" },
            new LegalAuthority("Fijian Competition and Consumer Commission Act 2010", "Part 7 — Consumer Protection and Unfair Practices", "https://www.laws.gov.fj/Acts/DisplayAct/2733", true)),

        (new[] { "employment", "employer", "dismiss", "salary", "wage", "workplace", "worker" },
            new LegalAuthority("Employment Relations Act 2007", "Employment relations and minimum labour standards", "https://www.laws.gov.fj/Acts/DisplayAct/2910", true)),

        (new[] { "domestic violence", "violence", "abuse", "protection order", "threat" },
            new LegalAuthority("Domestic Violence Act 2009", "Domestic violence restraining orders and related protections", "https://www.laws.gov.fj/Acts/DisplayAct/922", true)),

        (new[] { "family", "divorce", "custody", "maintenance", "marriage", "child support", "parenting" },
            new LegalAuthority("Family Law Act 2003", "Family law proceedings, parenting, support and property", "https://www.laws.gov.fj/Acts/DisplayAct/919", true)),

        (new[] { "criminal", "arrest", "police", "bail", "charge", "detained" },
            new LegalAuthority("Criminal Procedure Act 2009", "Criminal procedure, arrest powers and court process", "https://www.laws.gov.fj/Acts/DisplayAct/2622", true)),

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
            .DistinctBy(a => a.Title)
            .Take(6)
            .ToArray();

        return Task.FromResult<IReadOnlyList<LegalAuthority>>(results);
    }
}
