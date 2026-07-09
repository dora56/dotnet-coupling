namespace DotnetCoupling.Core;

public sealed record AnalysisSummary(
    string Path,
    string Mode,
    int Files,
    int Components,
    int InternalCouplings,
    int ExternalCouplings,
    bool GitRequested,
    bool GitUsed,
    int GitMonths);
