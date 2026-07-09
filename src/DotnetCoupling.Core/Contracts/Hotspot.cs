namespace DotnetCoupling.Core;

public sealed record Hotspot(
    int Rank,
    string Component,
    double Score,
    int IssueCount,
    int FanIn,
    int FanOut,
    Volatility Volatility,
    bool CrossesBoundary,
    bool ParticipatesInCycle,
    IReadOnlyList<string> Reasons,
    HotspotComplexity? Complexity = null);
