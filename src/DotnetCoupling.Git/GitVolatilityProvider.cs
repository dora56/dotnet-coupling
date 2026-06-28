using DotnetCoupling.Core;

namespace DotnetCoupling.Git;

public sealed class GitVolatilityProvider : IVolatilityProvider
{
    public VolatilityAnalysis Analyze(
        string repositoryPath,
        int months,
        IReadOnlySet<string> analyzedFiles,
        AnalysisOptions options)
    {
        IReadOnlyDictionary<string, int> changeCounts = GitVolatility.GetChangeCounts(repositoryPath, months);
        IReadOnlyList<TemporalCoupling> temporalCouplings = GitVolatility.GetTemporalCouplings(
            repositoryPath,
            months,
            analyzedFiles,
            options.Thresholds.MinTemporalCoupling,
            options.Thresholds.MaxTemporalFilesPerCommit);

        return new VolatilityAnalysis(changeCounts, temporalCouplings);
    }
}
