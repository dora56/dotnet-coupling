namespace DotnetCoupling.Core;

public static class TraceAnalyzer
{
    public static TraceAnalysis Analyze(AnalysisReport report, string query, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Trace depth must be non-negative.");
        }

        if (!string.Equals(report.Summary.Mode, "semantic-preview", StringComparison.Ordinal))
        {
            throw new InvestigationQueryException("Trace analysis requires semantic mode.");
        }

        Component? component = TryResolveComponent(report.Components, query);
        if (component is not null)
        {
            ImpactAnalysis impact = ImpactAnalyzer.Analyze(
                report,
                component.Id,
                maxDepth,
                PrioritizationOptions.Default);
            return new TraceAnalysis(query, component.Id, TracedSymbolKind.Component, impact.Dependents);
        }

        string member = ResolveMember(report.Observations, query);
        return new TraceAnalysis(
            query,
            member,
            TracedSymbolKind.Member,
            TraceMember(report, member, maxDepth));
    }

    private static Component? TryResolveComponent(IReadOnlyList<Component> components, string query)
    {
        bool hasPotentialMatch = components.Any(component =>
            string.Equals(component.Id, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(component.Name, query, StringComparison.OrdinalIgnoreCase));
        return hasPotentialMatch
            ? InvestigationTargetResolver.ResolveComponent(components, query)
            : null;
    }

    private static string ResolveMember(IReadOnlyList<DependencyObservation> observations, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvestigationQueryException("Investigation query must not be empty.");
        }

        string[] symbols = observations
            .Select(observation => observation.TargetSymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(symbol => symbol, StringComparer.Ordinal)
            .ToArray();
        string? exact = symbols.FirstOrDefault(symbol => string.Equals(symbol, query, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact;
        }

        string[] suffixMatches = symbols
            .Where(symbol => IsMemberSuffixMatch(symbol, query, StringComparison.Ordinal))
            .ToArray();
        if (suffixMatches.Length == 1)
        {
            return suffixMatches[0];
        }

        if (suffixMatches.Length > 1)
        {
            throw CreateAmbiguousException(query, suffixMatches);
        }

        string[] caseInsensitiveMatches = symbols
            .Where(symbol => IsMemberSuffixMatch(symbol, query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (caseInsensitiveMatches.Length == 1)
        {
            return caseInsensitiveMatches[0];
        }

        if (caseInsensitiveMatches.Length > 1)
        {
            throw CreateAmbiguousException(query, caseInsensitiveMatches);
        }

        string[] suggestions = symbols
            .Where(symbol => symbol.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToArray();
        string suffix = suggestions.Length == 0 ? "" : $" Candidates: {string.Join(", ", suggestions)}.";
        throw new InvestigationQueryException($"No semantic symbol matches '{query}'.{suffix}");
    }

    private static DependencyPath[] TraceMember(
        AnalysisReport report,
        string member,
        int maxDepth)
    {
        Dictionary<string, Component> components = report.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        DependencyObservation[] matchingObservations = report.Observations
            .Where(observation => string.Equals(observation.TargetSymbol, member, StringComparison.Ordinal))
            .Where(observation => components.ContainsKey(observation.SourceComponentId))
            .GroupBy(observation => observation.SourceComponentId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(observation => observation.FilePath, StringComparer.Ordinal).ThenBy(observation => observation.Line).First())
            .OrderBy(observation => observation.SourceComponentId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CouplingMetrics[]> incoming = report.Couplings
            .Where(coupling => components.ContainsKey(coupling.Source) && components.ContainsKey(coupling.Target))
            .GroupBy(coupling => coupling.Target, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(coupling => coupling.Source, StringComparer.Ordinal)
                    .Select(sourceGroup => sourceGroup.OrderBy(coupling => coupling.Location.File, StringComparer.Ordinal).ThenBy(coupling => coupling.Location.Line).First())
                    .OrderBy(coupling => coupling.Source, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        HashSet<string> visited = matchingObservations
            .Select(observation => observation.TargetName)
            .Concat(matchingObservations.Select(observation => observation.SourceComponentId))
            .ToHashSet(StringComparer.Ordinal);
        Queue<DependencyPath> queue = new();
        List<DependencyPath> callers = [];
        foreach (DependencyObservation observation in matchingObservations)
        {
            Component source = components[observation.SourceComponentId];
            CouplingMetrics? directCoupling = report.Couplings.FirstOrDefault(coupling =>
                string.Equals(coupling.Source, source.Id, StringComparison.Ordinal)
                && string.Equals(coupling.Target, observation.TargetName, StringComparison.Ordinal));
            DependencyPath direct = new(
                source.Id,
                1,
                [member, source.Id],
                source.ProjectName,
                source.Namespace,
                directCoupling?.Distance >= Distance.DifferentNamespace,
                new SourceLocation(observation.FilePath, observation.Line),
                observation.SourceSymbol);
            callers.Add(direct);
            queue.Enqueue(direct);
        }

        while (queue.TryDequeue(out DependencyPath? current))
        {
            if (maxDepth > 0 && current.Depth >= maxDepth)
            {
                continue;
            }

            foreach (CouplingMetrics coupling in incoming.GetValueOrDefault(current.Component, []))
            {
                if (!visited.Add(coupling.Source) || !components.TryGetValue(coupling.Source, out Component? component))
                {
                    continue;
                }

                DependencyPath transitive = new(
                    component.Id,
                    current.Depth + 1,
                    [.. current.Path, component.Id],
                    component.ProjectName,
                    component.Namespace,
                    current.CrossesBoundary || coupling.Distance >= Distance.DifferentNamespace,
                    coupling.Location);
                callers.Add(transitive);
                queue.Enqueue(transitive);
            }
        }

        return callers
            .OrderBy(caller => caller.Depth)
            .ThenBy(caller => caller.Component, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsMemberSuffixMatch(string symbol, string query, StringComparison comparison)
    {
        return symbol.EndsWith($".{query}", comparison)
            || string.Equals(symbol[(symbol.LastIndexOf('.') + 1)..], query, comparison);
    }

    private static InvestigationQueryException CreateAmbiguousException(string query, IEnumerable<string> candidates)
    {
        string candidateList = string.Join(", ", candidates.OrderBy(candidate => candidate, StringComparer.Ordinal));
        return new InvestigationQueryException($"Trace query '{query}' is ambiguous. Candidates: {candidateList}.");
    }
}
