namespace DotnetCoupling.Core;

public sealed record AnalysisOptions(
    IReadOnlyList<string> ExcludePathPatterns,
    IReadOnlyList<string> TestProjectPathPatterns,
    IReadOnlyList<string> IgnorePathPatterns,
    IReadOnlyList<string> IgnoreNamespaces,
    IReadOnlySet<IssueType> IgnoreIssueTypes,
    IReadOnlyList<IssueSuppression> IssueSuppressions,
    AnalysisThresholds Thresholds,
    DomainContext DomainContext)
{
    public static AnalysisOptions Default { get; } = new(
        [],
        [],
        [],
        [],
        new HashSet<IssueType>(),
        [],
        AnalysisThresholds.Default,
        DomainContext.Empty);
}
