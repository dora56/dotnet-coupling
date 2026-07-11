namespace DotnetCoupling.Core;

public sealed record TraceAnalysis(
    string Query,
    string Symbol,
    TracedSymbolKind Kind,
    IReadOnlyList<DependencyPath> Callers);
