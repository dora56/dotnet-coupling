namespace DotnetCoupling.Core;

public sealed record ImpactAnalysis(
    string Query,
    string Component,
    double RiskScore,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<DependencyPath> Dependents,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Namespaces,
    HotspotComplexity? Complexity);
