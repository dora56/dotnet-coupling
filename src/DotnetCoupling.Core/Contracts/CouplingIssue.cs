namespace DotnetCoupling.Core;

public sealed record CouplingIssue(
    IssueType Type,
    Severity Severity,
    string Source,
    string Target,
    double Score,
    string Problem,
    string Recommendation,
    SourceLocation? Location);
