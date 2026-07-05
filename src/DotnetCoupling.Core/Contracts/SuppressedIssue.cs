namespace DotnetCoupling.Core;

public sealed record SuppressedIssue(
    CouplingIssue Issue,
    string Reason);
