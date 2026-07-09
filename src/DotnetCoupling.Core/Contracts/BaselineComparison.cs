namespace DotnetCoupling.Core;

public sealed record BaselineComparison(
    string Ref,
    IReadOnlyList<CouplingIssue> NewIssues,
    IReadOnlyList<CouplingIssue> ResolvedIssues,
    IReadOnlyList<CouplingIssue> UnchangedIssues);
