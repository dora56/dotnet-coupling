namespace DotnetCoupling.Core;

internal sealed record RawConfiguration(
    RawAnalysis? Analysis,
    RawThresholds? Thresholds,
    RawIgnore? Ignore,
    RawDomain? Domain);

internal sealed record RawAnalysis(
    IReadOnlyList<string>? ExcludePathPatterns,
    IReadOnlyList<string>? TestProjectPathPatterns);

internal sealed record RawThresholds(
    int? MaxDependencies,
    int? MaxDependents,
    int? MinTemporalCoupling,
    int? MaxTemporalFilesPerCommit,
    int? ScatteredExternalBreadth);

internal sealed record RawIgnore(
    IReadOnlyList<string>? PathPatterns,
    IReadOnlyList<string>? Namespaces,
    IReadOnlyList<string>? IssueTypes,
    IReadOnlyList<RawIssueSuppression>? Issues);

internal sealed record RawIssueSuppression(
    string Type,
    string Source,
    string Target,
    string Reason);

internal sealed record RawDomain(IReadOnlyList<RawDomainSubdomain>? Subdomains);

internal sealed record RawDomainSubdomain(
    string Name,
    string Category,
    IReadOnlyList<string> PathPatterns,
    string ExpectedVolatility);
