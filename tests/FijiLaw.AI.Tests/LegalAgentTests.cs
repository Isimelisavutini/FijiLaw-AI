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
}
