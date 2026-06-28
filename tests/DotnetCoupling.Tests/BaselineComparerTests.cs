using DotnetCoupling.Core;
using DotnetCoupling.Git;
using DotnetCoupling.Roslyn;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class BaselineComparerTests
{
    [Fact]
    public void Compare_StableIssueKey_ClassifiesNewResolvedAndUnchangedIssues()
    {
        CouplingIssue unchangedCurrent = Issue(IssueType.GlobalComplexity, "Sample.Api.Handler", "Sample.Infrastructure.Repository", line: 99);
        CouplingIssue unchangedBaseline = Issue(IssueType.GlobalComplexity, "Sample.Api.Handler", "Sample.Infrastructure.Repository", line: 1);
        CouplingIssue newIssue = Issue(IssueType.CascadingChangeRisk, "Sample.Api.Handler", "Sample.Domain.Service", line: 10);
        CouplingIssue resolvedIssue = Issue(IssueType.CircularDependency, "Sample.A", "Sample.B", line: 20);

        AnalysisReport current = Report([unchangedCurrent, newIssue]);
        AnalysisReport baseline = Report([unchangedBaseline, resolvedIssue]);

        BaselineComparison comparison = BaselineComparer.Compare("main", current, baseline);

        Assert.Same(newIssue, Assert.Single(comparison.NewIssues));
        Assert.Same(resolvedIssue, Assert.Single(comparison.ResolvedIssues));
        Assert.Same(unchangedCurrent, Assert.Single(comparison.UnchangedIssues));
    }

    [Fact]
    public void Compare_HiddenCoupling_IsExcludedFromBaselineDiff()
    {
        AnalysisReport current = Report([Issue(IssueType.HiddenCoupling, "A.cs", "B.cs", line: 1)]);
        AnalysisReport baseline = Report([]);

        BaselineComparison comparison = BaselineComparer.Compare("main", current, baseline);

        Assert.Empty(comparison.NewIssues);
    }

    private static AnalysisReport Report(IReadOnlyList<CouplingIssue> issues)
    {
        return new AnalysisReport(
            new AnalysisSummary("/tmp/sample", "syntax-only", 1, 1, 1, 0, false, false, 6),
            new GradeResult("B", "Balanced", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            issues,
            []);
    }

    private static CouplingIssue Issue(IssueType type, string source, string target, int line)
    {
        return new CouplingIssue(
            type,
            Severity.High,
            source,
            target,
            0.2,
            "Problem",
            "Recommendation",
            new SourceLocation("/repo/File.cs", line));
    }
}
