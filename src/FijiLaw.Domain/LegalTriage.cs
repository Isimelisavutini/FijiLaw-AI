namespace FijiLaw.Domain;

public enum LegalRiskLevel { Low, Medium, High, Restricted }

public sealed record LegalTriageRequest(
    string Situation,
    string? Location = null,
    string Language = "en");

public sealed record LegalAuthority(
    string Title,
    string? Provision,
    string? SourceUrl,
    bool Verified);

public sealed record LegalTriageResult(
    string Issue,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<LegalAuthority> Authorities,
    string Guidance,
    IReadOnlyList<string> NextSteps,
    LegalRiskLevel RiskLevel,
    bool HumanReviewRequired,
    string Disclaimer,
    string CorrelationId);
