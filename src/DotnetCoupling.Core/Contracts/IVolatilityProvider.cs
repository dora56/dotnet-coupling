namespace DotnetCoupling.Core;

public interface IVolatilityProvider
{
    VolatilityAnalysis Analyze(
        string repositoryPath,
        int months,
        IReadOnlySet<string> analyzedFiles,
        AnalysisOptions options);
}
