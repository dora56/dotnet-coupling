namespace DotnetCoupling.Core;

public sealed record IssueSuppression(
    IssueType Type,
    string Source,
    string Target,
    string Reason);
