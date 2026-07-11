namespace DotnetCoupling.Core;

public static class ImpactAnalyzer
{
    public static ImpactAnalysis Analyze(
        AnalysisReport report,
        string query,
        int maxDepth,
        PrioritizationOptions prioritization)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(prioritization);
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Impact depth must be non-negative.");
        }

        Component target = InvestigationTargetResolver.ResolveComponent(report.Components, query);
        Dictionary<string, Component> components = report.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        Dictionary<string, CouplingMetrics[]> incoming = report.Couplings
            .Where(coupling => components.ContainsKey(coupling.Source) && components.ContainsKey(coupling.Target))
            .GroupBy(coupling => coupling.Target, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(coupling => coupling.Source, StringComparer.Ordinal)
                    .Select(sourceGroup => sourceGroup
                        .OrderBy(coupling => coupling.Location.File, StringComparer.Ordinal)
                        .ThenBy(coupling => coupling.Location.Line)
                        .First())
                    .OrderBy(coupling => coupling.Source, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        List<DependencyPath> dependents = Traverse(target.Id, maxDepth, incoming, components);
        HashSet<(string Target, string Source)> traversedEdges = dependents
            .SelectMany(dependent => dependent.Path.Zip(
                dependent.Path.Skip(1),
                (edgeTarget, edgeSource) => (Target: edgeTarget, Source: edgeSource)))
            .ToHashSet();
        CouplingMetrics[] traversedCouplings = report.Couplings
            .Where(coupling => traversedEdges.Contains((coupling.Target, coupling.Source)))
            .Distinct()
            .ToArray();
        ComponentComplexity? complexity = report.ComponentComplexities?
            .FirstOrDefault(item => string.Equals(item.ComponentId, target.Id, StringComparison.Ordinal));
        double riskScore = CalculateRisk(dependents, traversedCouplings, complexity, prioritization);
        List<string> reasons = CreateReasons(dependents, traversedCouplings, complexity, prioritization);

        return new ImpactAnalysis(
            query,
            target.Id,
            riskScore,
            reasons,
            dependents,
            dependents
                .Select(dependent => dependent.Project)
                .Where(project => !string.IsNullOrWhiteSpace(project))
                .Select(project => project!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(project => project, StringComparer.Ordinal)
                .ToArray(),
            dependents
                .Select(dependent => dependent.Namespace)
                .Where(namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(namespaceName => namespaceName, StringComparer.Ordinal)
                .ToArray(),
            ComplexityPrioritizer.CreateSummary(complexity));
    }

    private static List<DependencyPath> Traverse(
        string target,
        int maxDepth,
        Dictionary<string, CouplingMetrics[]> incoming,
        Dictionary<string, Component> components)
    {
        Queue<(string Component, IReadOnlyList<string> Path, bool CrossesBoundary)> queue = new();
        queue.Enqueue((target, [target], false));
        HashSet<string> visited = new(StringComparer.Ordinal) { target };
        List<DependencyPath> dependents = [];

        while (queue.TryDequeue(out (string Component, IReadOnlyList<string> Path, bool CrossesBoundary) current))
        {
            int currentDepth = current.Path.Count - 1;
            if (maxDepth > 0 && currentDepth >= maxDepth)
            {
                continue;
            }

            foreach (CouplingMetrics coupling in incoming.GetValueOrDefault(current.Component, []))
            {
                if (!visited.Add(coupling.Source) || !components.TryGetValue(coupling.Source, out Component? component))
                {
                    continue;
                }

                string[] path = [.. current.Path, coupling.Source];
                bool crossesBoundary = current.CrossesBoundary || coupling.Distance >= Distance.DifferentNamespace;
                dependents.Add(new DependencyPath(
                    component.Id,
                    path.Length - 1,
                    path,
                    component.ProjectName,
                    component.Namespace,
                    crossesBoundary,
                    coupling.Location));
                queue.Enqueue((component.Id, path, crossesBoundary));
            }
        }

        return dependents
            .OrderBy(dependent => dependent.Depth)
            .ThenBy(dependent => dependent.Component, StringComparer.Ordinal)
            .ToList();
    }

    private static double CalculateRisk(
        List<DependencyPath> dependents,
        CouplingMetrics[] couplings,
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
    {
        double reach = Math.Min(1.0, dependents.Count / 10.0);
        double couplingRisk = couplings.Length == 0
            ? 0
            : couplings.Max(coupling => 1.0 - CouplingScoring.Calculate(coupling).Score);
        double volatility = couplings.Length == 0
            ? 0
            : couplings.Max(coupling => VolatilityRank(coupling.Volatility)) / 2.0;
        double boundary = dependents.Any(dependent => dependent.CrossesBoundary) ? 1.0 : 0.0;
        double complexityPressure = ComplexityPrioritizer.CalculatePressure(complexity, prioritization);
        return Math.Clamp(
            reach * 0.40
            + couplingRisk * 0.25
            + volatility * 0.20
            + boundary * 0.15
            + complexityPressure * prioritization.ComplexityWeight,
            0,
            1);
    }

    private static List<string> CreateReasons(
        List<DependencyPath> dependents,
        CouplingMetrics[] couplings,
        ComponentComplexity? complexity,
        PrioritizationOptions prioritization)
    {
        List<string> reasons =
        [
            $"{dependents.Count} affected component(s)",
            $"{dependents.Count(dependent => dependent.Depth == 1)} direct dependent(s)",
        ];
        if (dependents.Any(dependent => dependent.CrossesBoundary))
        {
            reasons.Add("crosses namespace or project boundary");
        }

        if (couplings.Any(coupling => coupling.Volatility == Volatility.High))
        {
            reasons.Add("high volatility");
        }

        reasons.AddRange(ComplexityPrioritizer.CreateReasons(complexity, prioritization));

        return reasons;
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
