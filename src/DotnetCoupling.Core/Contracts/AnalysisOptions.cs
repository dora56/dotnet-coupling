namespace DotnetCoupling.Core;

public sealed record AnalysisOptions(
    IReadOnlyList<string> ExcludePathPatterns,
    IReadOnlyList<string> TestProjectPathPatterns,
    IReadOnlyList<string> IgnorePathPatterns,
    IReadOnlyList<string> IgnoreNamespaces,
    IReadOnlySet<IssueType> IgnoreIssueTypes,
    IReadOnlyList<IssueSuppression> IssueSuppressions,
    AnalysisThresholds Thresholds,
    DomainContext DomainContext,
    PrioritizationOptions Prioritization)
{
    public AnalysisOptions(
        IReadOnlyList<string> excludePathPatterns,
        IReadOnlyList<string> testProjectPathPatterns,
        IReadOnlyList<string> ignorePathPatterns,
        IReadOnlyList<string> ignoreNamespaces,
        IReadOnlySet<IssueType> ignoreIssueTypes,
        IReadOnlyList<IssueSuppression> issueSuppressions,
        AnalysisThresholds thresholds,
        DomainContext domainContext)
        : this(
            excludePathPatterns,
            testProjectPathPatterns,
            ignorePathPatterns,
            ignoreNamespaces,
            ignoreIssueTypes,
            issueSuppressions,
            thresholds,
            domainContext,
            PrioritizationOptions.Default)
    {
    }

    public static AnalysisOptions Default { get; } = new(
        [],
        [],
        [],
        [],
        new HashSet<IssueType>(),
        [],
        AnalysisThresholds.Default,
        DomainContext.Empty,
        PrioritizationOptions.Default);
}
