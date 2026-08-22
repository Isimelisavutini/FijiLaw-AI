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

public sealed record AuthorityAnalysis(
    string Category,
    string AuthorityReference,
    string Relevance,
    string? SourceUrl,
    bool Verified);

public sealed record VulnerabilityItem(
    string CauseOfAction,
    string LegalThreshold,
    string FactApplied,
    string RiskRating);

public sealed record ProceduralRoadmap(
    string? DecisionDate,
    string? AwarenessDate,
    string LimitationStatus,
    string? VerifiedRule,
    string? EstimatedExpiry,
    int? DaysRemaining,
    IReadOnlyList<string> Steps);

public sealed record EvidenceGapAnalysis(
    IReadOnlyList<string> FactsEstablished,
    IReadOnlyList<string> DocumentsAvailable,
    IReadOnlyList<string> CriticalMissingDocuments,
    IReadOnlyList<string> MissingDates,
    IReadOnlyList<string> FactsRequiringVerification);

public sealed record VerificationStatement(
    string RetrievalConfidence,
    int VerifiedAuthorityCount,
    IReadOnlyList<string> AuthoritiesRequiringVerification,
    IReadOnlyList<string> ConclusionsDependingOnMissingFacts,
    string GeneratedAtUtc);

public sealed record AdvancedLegalTriageReport(
    string ReportTitle,
    string CaseReference,
    string Priority,
    string Jurisdiction,
    string PrimaryArea,
    IReadOnlyList<string> SecondaryAreas,
    IReadOnlyList<string> DoctrinalTags,
    IReadOnlyList<AuthorityAnalysis> Authorities,
    IReadOnlyList<VulnerabilityItem> VulnerabilityMatrix,
    ProceduralRoadmap ProceduralRoadmap,
    EvidenceGapAnalysis EvidenceGaps,
    IReadOnlyList<string> ImmediateActions,
    string HumanLawyerEscalation,
    VerificationStatement Verification,
    string Disclaimer);

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
    string CorrelationId,
    IReadOnlyList<string>? LegalDomains = null,
    AdvancedLegalTriageReport? AdvancedReport = null);
