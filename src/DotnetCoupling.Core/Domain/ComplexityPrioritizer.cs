namespace DotnetCoupling.Core;

internal static class ComplexityPrioritizer
{
    private const double CyclomaticPressureRange = 20.0;
    private const double CognitivePressureRange = 30.0;

    internal static double CalculatePressure(
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
    {
        if (complexity is null)
        {
            return 0;
        }

        double cyclomatic = Math.Clamp(
            (complexity.MaxCyclomaticComplexity - prioritization.CyclomaticComplexityThreshold + 1)
            / CyclomaticPressureRange,
            0,
            1);
        double cognitive = Math.Clamp(
            (complexity.MaxCognitiveComplexity - prioritization.CognitiveComplexityThreshold + 1)
            / CognitivePressureRange,
            0,
            1);
        return Math.Max(cyclomatic, cognitive);
    }

    internal static IReadOnlyList<string> CreateReasons(
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
    {
        if (complexity is null)
        {
            return [];
        }

        List<string> reasons = [];
        if (complexity.MaxCyclomaticComplexity >= prioritization.CyclomaticComplexityThreshold)
        {
            reasons.Add($"high cyclomatic complexity: {complexity.MaxCyclomaticComplexity}");
        }

        if (complexity.MaxCognitiveComplexity >= prioritization.CognitiveComplexityThreshold)
        {
            reasons.Add($"high cognitive complexity: {complexity.MaxCognitiveComplexity}");
        }

        return reasons;
    }

    internal static HotspotComplexity? CreateSummary(ComponentComplexity? complexity)
    {
        return complexity is null
            ? null
            : new HotspotComplexity(
                complexity.MaxCyclomaticComplexity,
                complexity.MaxCognitiveComplexity,
                complexity.TotalCyclomaticComplexity,
                complexity.TotalCognitiveComplexity,
                complexity.MemberCount,
                complexity.MostComplexMember?.MemberName,
                complexity.MostComplexMember?.Location);
    }
}
