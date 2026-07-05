namespace DotnetCoupling.Core;

public sealed record AnalysisThresholds(
    int MaxDependencies,
    int MaxDependents,
    int MinTemporalCoupling,
    int MaxTemporalFilesPerCommit,
    int ScatteredExternalBreadth)
{
    public static AnalysisThresholds Default { get; } = new(
        20,
        30,
        3,
        50,
        5);
}
