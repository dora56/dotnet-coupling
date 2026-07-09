namespace DotnetCoupling.Core;

public sealed record BalanceScore(
    CouplingMetrics Coupling,
    double Score,
    double Alignment,
    double VolatilityImpact,
    string Interpretation);
