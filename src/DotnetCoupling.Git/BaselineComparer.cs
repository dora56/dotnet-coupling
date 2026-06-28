using DotnetCoupling.Core;

namespace DotnetCoupling.Git;

public static class BaselineComparer
{
    public static BaselineComparison Compare(string baselineRef, AnalysisReport current, AnalysisReport baseline)
    {
        Dictionary<IssueKey, CouplingIssue> baselineIssues = baseline.Issues
            .Where(issue => issue.Type != IssueType.HiddenCoupling)
            .ToDictionary(CreateKey, issue => issue);
        Dictionary<IssueKey, CouplingIssue> currentIssues = current.Issues
            .Where(issue => issue.Type != IssueType.HiddenCoupling)
            .ToDictionary(CreateKey, issue => issue);

        List<CouplingIssue> newIssues = currentIssues
            .Where(pair => !baselineIssues.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Type)
            .ThenBy(issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(issue => issue.Target, StringComparer.Ordinal)
            .ToList();

        List<CouplingIssue> resolvedIssues = baselineIssues
            .Where(pair => !currentIssues.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Type)
            .ThenBy(issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(issue => issue.Target, StringComparer.Ordinal)
            .ToList();

        List<CouplingIssue> unchangedIssues = currentIssues
            .Where(pair => baselineIssues.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Type)
            .ThenBy(issue => issue.Source, StringComparer.Ordinal)
            .ThenBy(issue => issue.Target, StringComparer.Ordinal)
            .ToList();

        return new BaselineComparison(baselineRef, newIssues, resolvedIssues, unchangedIssues);
    }

    public static AnalysisReport WithBaseline(AnalysisReport report, BaselineComparison baseline)
    {
        return report with { Baseline = baseline };
    }

    private static IssueKey CreateKey(CouplingIssue issue)
    {
        return new IssueKey(issue.Type, issue.Source, issue.Target);
    }

    private sealed record IssueKey(IssueType Type, string Source, string Target);
}
