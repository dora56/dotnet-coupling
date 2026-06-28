namespace DotnetCoupling.Core;

public interface IVolatilityProvider
{
    VolatilityAnalysis Analyze(
        string repositoryPath,
        int months,
        IReadOnlySet<string> analyzedFiles,
        AnalysisOptions options);
}

public sealed record VolatilityAnalysis(
    IReadOnlyDictionary<string, int> ChangeCounts,
    IReadOnlyList<TemporalCoupling> TemporalCouplings)
{
    public static VolatilityAnalysis Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        []);
}
