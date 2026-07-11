namespace DotnetCoupling.Core;

public sealed record PrioritizationOptions(
    int CyclomaticComplexityThreshold,
    int CognitiveComplexityThreshold,
    double ComplexityWeight)
{
    public static PrioritizationOptions Default { get; } = new(10, 15, 0.10);
}
