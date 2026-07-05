namespace DotnetCoupling.Core;

public sealed record VolatilityAnalysis(
    IReadOnlyDictionary<string, int> ChangeCounts,
    IReadOnlyList<TemporalCoupling> TemporalCouplings)
{
    public static VolatilityAnalysis Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        []);
}
