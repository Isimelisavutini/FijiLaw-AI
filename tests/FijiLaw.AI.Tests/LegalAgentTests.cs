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
}
