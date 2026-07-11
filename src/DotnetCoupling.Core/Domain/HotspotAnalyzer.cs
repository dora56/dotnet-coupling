namespace DotnetCoupling.Core;

public static class HotspotAnalyzer
{
    public const int DefaultCount = 10;
    public const int CyclomaticWarningThreshold = 10;
    public const int CognitiveWarningThreshold = 15;

    public static IReadOnlyList<Hotspot> Calculate(AnalysisReport report)
    {
        return Calculate(report, DefaultCount);
    }

    public static IReadOnlyList<Hotspot> Calculate(AnalysisReport report, int count)
    {
        return Calculate(report, count, PrioritizationOptions.Default);
    }

    public static IReadOnlyList<Hotspot> Calculate(
        AnalysisReport report,
        int count,
        PrioritizationOptions prioritization)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Hotspot count must be positive.");
        }

        Dictionary<string, List<CouplingIssue>> issuesByComponent = new(StringComparer.Ordinal);
        foreach (CouplingIssue issue in report.Issues)
        {
            if (issue.Type == IssueType.CircularDependency)
            {
                foreach (string participant in SplitCycleComponents(issue.Source)
                    .Concat(SplitCycleComponents(issue.Target))
                    .Distinct(StringComparer.Ordinal))
                {
                    AddIssue(issuesByComponent, participant, issue);
                }

                continue;
            }

            AddIssue(issuesByComponent, issue.Source, issue);
            if (!string.Equals(issue.Source, issue.Target, StringComparison.Ordinal))
            {
                AddIssue(issuesByComponent, issue.Target, issue);
            }
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
        Dictionary<string, ComponentRoleContext> roleContexts = (report.ComponentRoles ?? [])
            .GroupBy(context => context.ComponentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, ComponentComplexity> complexities = CreateComplexityLookup(report.ComponentComplexities ?? []);

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
                ComponentComplexity? componentComplexity = complexities.GetValueOrDefault(component);
                HotspotComplexity? hotspotComplexity = ComplexityPrioritizer.CreateSummary(componentComplexity);
                double score = CalculatePriorityScore(
                    issues,
                    componentFanIn,
                    componentFanOut,
                    componentVolatility,
                    crossesBoundary,
                    participatesInCycle,
                    componentComplexity,
                    prioritization);

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
                    Reasons: CreateReasons(
                        issues,
                        componentFanIn,
                        componentFanOut,
                        componentVolatility,
                        crossesBoundary,
                        participatesInCycle,
                        roleContexts.GetValueOrDefault(component),
                        componentComplexity,
                        prioritization),
                    Complexity: hotspotComplexity);
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
        bool participatesInCycle,
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
    {
        double severity = issues.Count == 0 ? 0.0 : issues.Max(issue => SeverityRank(issue.Severity)) / 3.0;
        double issueDensity = Math.Min(1.0, issues.Count / 5.0);
        double weakScore = issues.Count == 0 ? 0.0 : issues.Max(issue => 1.0 - issue.Score);
        double fan = Math.Min(1.0, (fanIn + fanOut) / 20.0);
        double volatilityScore = VolatilityRank(volatility) / 2.0;
        double boundary = crossesBoundary ? 0.10 : 0.0;
        double cycle = participatesInCycle ? 0.15 : 0.0;
        double complexityBonus = ComplexityPrioritizer.CalculatePressure(complexity, prioritization)
            * prioritization.ComplexityWeight;

        return Math.Clamp(
            severity * 0.35
            + weakScore * 0.25
            + issueDensity * 0.15
            + fan * 0.10
            + volatilityScore * 0.05
            + boundary
            + cycle
            + complexityBonus,
            0.0,
            1.0);
    }

    private static List<string> CreateReasons(
        List<CouplingIssue> issues,
        int fanIn,
        int fanOut,
        Volatility volatility,
        bool crossesBoundary,
        bool participatesInCycle,
        ComponentRoleContext? roleContext,
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
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

        reasons.AddRange(ComplexityPrioritizer.CreateReasons(complexity, prioritization));

        if (roleContext?.TechnicalRole is TechnicalRole technicalRole)
        {
            reasons.Add($"technical role: {ToSnakeCase(technicalRole.ToString())}");
        }

        if (roleContext?.StrategicRole is StrategicRole strategicRole)
        {
            reasons.Add($"strategic role: {ToSnakeCase(strategicRole.ToString())}");
        }

        return reasons;
    }

    private static Dictionary<string, ComponentComplexity> CreateComplexityLookup(IReadOnlyList<ComponentComplexity> complexities)
    {
        Dictionary<string, ComponentComplexity> lookup = complexities
            .GroupBy(complexity => complexity.ComponentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (IGrouping<string, ComponentComplexity> fileGroup in complexities.GroupBy(complexity => complexity.FilePath, StringComparer.Ordinal))
        {
            if (!lookup.ContainsKey(fileGroup.Key))
            {
                lookup[fileGroup.Key] = MergeFileComplexities(fileGroup.Key, fileGroup);
            }
        }

        return lookup;
    }

    private static ComponentComplexity MergeFileComplexities(string filePath, IEnumerable<ComponentComplexity> complexities)
    {
        ComponentComplexity[] fileComplexities = complexities.ToArray();
        MemberComplexity? mostComplexMember = fileComplexities
            .Select(complexity => complexity.MostComplexMember)
            .OfType<MemberComplexity>()
            .OrderByDescending(member => member.CognitiveComplexity)
            .ThenByDescending(member => member.CyclomaticComplexity)
            .ThenBy(member => member.MemberName, StringComparer.Ordinal)
            .FirstOrDefault();

        return new ComponentComplexity(
            filePath,
            filePath,
            fileComplexities.Sum(complexity => complexity.MemberCount),
            fileComplexities.Length == 0 ? 0 : fileComplexities.Max(complexity => complexity.MaxCyclomaticComplexity),
            fileComplexities.Length == 0 ? 0 : fileComplexities.Max(complexity => complexity.MaxCognitiveComplexity),
            fileComplexities.Sum(complexity => complexity.TotalCyclomaticComplexity),
            fileComplexities.Sum(complexity => complexity.TotalCognitiveComplexity),
            mostComplexMember);
    }

    private static string[] SplitCycleComponents(string value)
    {
        return value.Split(" -> ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ToSnakeCase(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        List<char> characters = [];
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                characters.Add('_');
            }

            characters.Add(char.ToLowerInvariant(character));
        }

        return new string(characters.ToArray());
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
