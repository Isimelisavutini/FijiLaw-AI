using FijiLaw.AI;
using FijiLaw.Domain;

namespace FijiLaw.AI.Tests;

public class LegalAgentTests
{
    private readonly LegalAgent _agent = new(new EmptyLegalSourceRetriever());

    [Fact]
    public async Task Employment_issue_is_classified_without_fake_authorities()
    {
        var result = await _agent.TriageAsync(new LegalTriageRequest("My employer dismissed me without giving a written reason."));
        Assert.Equal("Employment", result.Issue);
        Assert.Empty(result.Authorities);
        Assert.Contains("will not invent", result.Guidance);
    }

    [Fact]
    public async Task Arrest_is_high_risk_and_requires_human_review()
    {
        var result = await _agent.TriageAsync(new LegalTriageRequest("Police arrested my brother and he is detained."));
        Assert.Equal(LegalRiskLevel.High, result.RiskLevel);
        Assert.True(result.HumanReviewRequired);
    }

    [Fact]
    public async Task Faulty_goods_retrieve_section_level_consumer_authority()
    {
        var retriever = new CuratedFijiLegalSourceRetriever();
        var authorities = await retriever.SearchAsync("The fridge I bought is faulty and the retailer refuses a refund.");

        Assert.Contains(authorities, a =>
            a.Title == "Fijian Competition and Consumer Commission Act 2010" &&
            a.Provision is not null &&
            a.Provision.Contains("s 114"));
    }

    [Fact]
    public async Task Summary_dismissal_retrieves_section_33()
    {
        var retriever = new CuratedFijiLegalSourceRetriever();
        var authorities = await retriever.SearchAsync("My employer dismissed me immediately for alleged misconduct.");

        Assert.Contains(authorities, a =>
            a.Title == "Employment Relations Act 2007" &&
            a.Provision is not null &&
            a.Provision.Contains("s 33"));
    }

    [Fact]
    public async Task Ficac_appointment_dispute_is_multi_label_public_law()
    {
        var agent = new LegalAgent(new CuratedFijiLegalSourceRetriever());
        var result = await agent.TriageAsync(new LegalTriageRequest(
            "I want to challenge the constitutionality of a FICAC Commissioner appointment recommended by the JSC and consider judicial review."));

        Assert.Equal("Constitutional / Public & Administrative Law", result.Issue);
        Assert.NotNull(result.LegalDomains);
        Assert.Contains("Constitutional Law", result.LegalDomains!);
        Assert.Contains("Public & Administrative Law", result.LegalDomains!);
        Assert.Contains("Public Governance", result.LegalDomains!);
    }

    [Fact]
    public async Task Ficac_governance_query_excludes_family_and_domestic_violence_sources()
    {
        var retriever = new CuratedFijiLegalSourceRetriever();
        var authorities = await retriever.SearchAsync(
            "There is a constitutional dispute about the FICAC Commissioner appointment by the President following a JSC recommendation and I am considering judicial review.");

        Assert.Contains(authorities, a => a.Title == "Fiji Independent Commission Against Corruption Act 2007");
        Assert.Contains(authorities, a => a.Title == "High Court Rules 1988");
        Assert.DoesNotContain(authorities, a => a.Title == "Domestic Violence Act 2009");
        Assert.DoesNotContain(authorities, a => a.Title == "Family Law Act 2003");
        Assert.DoesNotContain(authorities, a => a.Title == "Fijian Competition and Consumer Commission Act 2010");
    }

    [Fact]
    public async Task Public_law_missing_information_is_context_specific()
    {
        var agent = new LegalAgent(new CuratedFijiLegalSourceRetriever());
        var result = await agent.TriageAsync(new LegalTriageRequest(
            "I am disputing a FICAC appointment involving the JSC and want judicial review."));

        Assert.Contains(result.MissingInformation, x => x.Contains("appointment instrument", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.NextSteps, x => x.Contains("Order 53", StringComparison.OrdinalIgnoreCase));
    }
}
