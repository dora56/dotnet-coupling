namespace DotnetCoupling.Core;

public static class HotspotAnalyzer
{
    public const int DefaultCount = 10;

    public static IReadOnlyList<Hotspot> Calculate(AnalysisReport report, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Hotspot count must be positive.");
        }

        Dictionary<string, List<CouplingIssue>> issuesByComponent = new(StringComparer.Ordinal);
        foreach (CouplingIssue issue in report.Issues)
        {
            AddIssue(issuesByComponent, issue.Source, issue);
            AddIssue(issuesByComponent, issue.Target, issue);
        }

        Dictionary<string, int> fanOut = report.Couplings
            .GroupBy(coupling => coupling.Source, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(coupling => coupling.Target).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);
        Dictionary<string, int> fanIn = report.Couplings
            .GroupBy(coupling => coupling.Target, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(coupling => coupling.Source).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);
        Dictionary<string, Volatility> volatility = CalculateVolatility(report.Couplings);
        HashSet<string> boundaryCrossingComponents = report.Couplings
            .Where(coupling => coupling.Distance >= Distance.DifferentNamespace)
            .SelectMany(coupling => new[] { coupling.Source, coupling.Target })
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> cycleComponents = report.Issues
            .Where(issue => issue.Type == IssueType.CircularDependency)
            .SelectMany(issue => SplitCycleComponents(issue.Source).Concat(SplitCycleComponents(issue.Target)))
            .ToHashSet(StringComparer.Ordinal);

        return issuesByComponent
            .Select(pair =>
            {
                string component = pair.Key;
                List<CouplingIssue> issues = pair.Value;
                int componentFanIn = fanIn.GetValueOrDefault(component);
                int componentFanOut = fanOut.GetValueOrDefault(component);
                Volatility componentVolatility = volatility.GetValueOrDefault(component, Volatility.Low);
                bool crossesBoundary = boundaryCrossingComponents.Contains(component);
                bool participatesInCycle = cycleComponents.Contains(component);
                double score = CalculatePriorityScore(
                    issues,
                    componentFanIn,
                    componentFanOut,
                    componentVolatility,
                    crossesBoundary,
                    participatesInCycle);

                return new Hotspot(
                    Rank: 0,
                    Component: component,
                    Score: score,
                    IssueCount: issues.Count,
                    FanIn: componentFanIn,
                    FanOut: componentFanOut,
                    Volatility: componentVolatility,
                    CrossesBoundary: crossesBoundary,
                    ParticipatesInCycle: participatesInCycle,
                    Reasons: CreateReasons(issues, componentFanIn, componentFanOut, componentVolatility, crossesBoundary, participatesInCycle));
            })
            .OrderByDescending(hotspot => hotspot.Score)
            .ThenByDescending(hotspot => hotspot.IssueCount)
            .ThenBy(hotspot => hotspot.Component, StringComparer.Ordinal)
            .Take(count)
            .Select((hotspot, index) => hotspot with { Rank = index + 1 })
            .ToArray();
    }

    private static void AddIssue(Dictionary<string, List<CouplingIssue>> issuesByComponent, string component, CouplingIssue issue)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            return;
        }

        if (!issuesByComponent.TryGetValue(component, out List<CouplingIssue>? issues))
        {
            issues = [];
            issuesByComponent[component] = issues;
        }

        issues.Add(issue);
    }

    private static Dictionary<string, Volatility> CalculateVolatility(IReadOnlyList<CouplingMetrics> couplings)
    {
        Dictionary<string, Volatility> volatility = new(StringComparer.Ordinal);
        foreach (CouplingMetrics coupling in couplings)
        {
            SetMaxVolatility(volatility, coupling.Source, coupling.Volatility);
            SetMaxVolatility(volatility, coupling.Target, coupling.Volatility);
        }

        return volatility;
    }

    private static void SetMaxVolatility(Dictionary<string, Volatility> values, string component, Volatility volatility)
    {
        if (!values.TryGetValue(component, out Volatility current) || VolatilityRank(volatility) > VolatilityRank(current))
        {
            values[component] = volatility;
        }
    }

    private static double CalculatePriorityScore(
        List<CouplingIssue> issues,
        int fanIn,
        int fanOut,
        Volatility volatility,
        bool crossesBoundary,
        bool participatesInCycle)
    {
        double severity = issues.Count == 0 ? 0.0 : issues.Max(issue => SeverityRank(issue.Severity)) / 3.0;
        double issueDensity = Math.Min(1.0, issues.Count / 5.0);
        double weakScore = issues.Count == 0 ? 0.0 : issues.Max(issue => 1.0 - issue.Score);
        double fan = Math.Min(1.0, (fanIn + fanOut) / 20.0);
        double volatilityScore = VolatilityRank(volatility) / 2.0;
        double boundary = crossesBoundary ? 0.10 : 0.0;
        double cycle = participatesInCycle ? 0.15 : 0.0;

        return Math.Clamp(
            severity * 0.35
            + weakScore * 0.25
            + issueDensity * 0.15
            + fan * 0.10
            + volatilityScore * 0.05
            + boundary
            + cycle,
            0.0,
            1.0);
    }

    private static List<string> CreateReasons(
        List<CouplingIssue> issues,
        int fanIn,
        int fanOut,
        Volatility volatility,
        bool crossesBoundary,
        bool participatesInCycle)
    {
        List<string> reasons = [];
        Severity maxSeverity = issues.Count == 0 ? Severity.Low : issues.Max(issue => issue.Severity);
        reasons.Add($"{issues.Count} active issue(s)");
        if (maxSeverity >= Severity.High)
        {
            reasons.Add($"{maxSeverity} severity issue");
        }

        if (fanIn + fanOut > 0)
        {
            reasons.Add($"{fanIn} incoming / {fanOut} outgoing dependencies");
        }

        if (volatility == Volatility.High)
        {
            reasons.Add("high volatility");
        }

        if (crossesBoundary)
        {
            reasons.Add("crosses namespace or project boundary");
        }

        if (participatesInCycle)
        {
            reasons.Add("participates in a cycle");
        }

        return reasons;
    }

    private static string[] SplitCycleComponents(string value)
    {
        return value.Split(" -> ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int SeverityRank(Severity severity)
    {
        return severity switch
        {
            Severity.Low => 0,
            Severity.Medium => 1,
            Severity.High => 2,
            Severity.Critical => 3,
            _ => 1,
        };
    }

    private static int VolatilityRank(Volatility volatility)
    {
        return volatility switch
        {
            Volatility.Low => 0,
            Volatility.Medium => 1,
            Volatility.High => 2,
            _ => 0,
        };
    }
}
