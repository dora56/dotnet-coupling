namespace DotnetCoupling.Core;

internal static class IssueDetector
{
    internal const int MaxDependencies = 20;
    internal const int MaxDependents = 30;
    internal const int ScatteredExternalBreadth = 5;
    internal const int HiddenCouplingHighThreshold = 6;

    internal static List<CouplingIssue> DetectIssues(
        IReadOnlyCollection<BalanceScore> scores,
        IReadOnlyList<TemporalCoupling> temporalCouplings,
        IReadOnlyDictionary<string, Component> componentsById,
        AnalysisOptions? options = null)
    {
        options ??= AnalysisOptions.Default;
        List<CouplingIssue> issues = [];

        foreach (BalanceScore score in scores)
        {
            CouplingMetrics coupling = score.Coupling;
            if (coupling.Strength >= IntegrationStrength.Functional && coupling.Distance >= Distance.DifferentNamespace)
            {
                issues.Add(new CouplingIssue(
                    IssueType.GlobalComplexity,
                    score.Score < 0.40 ? Severity.High : Severity.Medium,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Strong coupling spans a namespace or project boundary.",
                    "Introduce an interface, move the dependency closer, or add a port/adapter.",
                    coupling.Location));
            }

            if (coupling.Strength >= IntegrationStrength.Functional && coupling.Volatility == Volatility.High)
            {
                issues.Add(new CouplingIssue(
                    IssueType.CascadingChangeRisk,
                    Severity.High,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Strong coupling targets a frequently changing component.",
                    "Stabilize the target API, introduce an interface, or invert the dependency.",
                    coupling.Location));
            }

            if (coupling.Strength == IntegrationStrength.Intrusive && coupling.Distance >= Distance.DifferentNamespace)
            {
                issues.Add(new CouplingIssue(
                    IssueType.InappropriateIntimacy,
                    Severity.High,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Intrusive implementation-detail access crosses a boundary.",
                    "Encapsulate the implementation detail behind a stable API.",
                    coupling.Location));
            }
        }

        AddFanInFanOutIssues(scores.Select(score => score.Coupling), issues, options.Thresholds);
        AddCircularDependencyIssues(scores.Select(score => score.Coupling), issues);
        AddHiddenCouplingIssues(temporalCouplings, scores.Select(score => score.Coupling), componentsById, issues);
        AddScatteredExternalCouplingIssues(scores.Select(score => score.Coupling), issues, options.Thresholds);
        return ApplyIgnores(issues, options)
            .GroupBy(issue => new IssueIdentity(issue.Type, issue.Source, issue.Target))
            .Select(group => group
                .OrderBy(issue => issue.Location?.File ?? "", StringComparer.Ordinal)
                .ThenBy(issue => issue.Location?.Line ?? 0)
                .First())
            .ToList();
    }

    internal static void AddFanInFanOutIssues(
        IEnumerable<CouplingMetrics> couplings,
        List<CouplingIssue> issues,
        AnalysisThresholds? thresholds = null)
    {
        thresholds ??= AnalysisThresholds.Default;
        foreach (IGrouping<string, CouplingMetrics> group in couplings.GroupBy(coupling => coupling.Source))
        {
            int count = group.Select(coupling => coupling.Target).Distinct(StringComparer.Ordinal).Count();
            if (count > thresholds.MaxDependencies)
            {
                issues.Add(new CouplingIssue(
                    IssueType.HighEfferentCoupling,
                    count > thresholds.MaxDependencies * 2 ? Severity.High : Severity.Medium,
                    group.Key,
                    "",
                    1.0,
                    "Component depends on too many distinct targets.",
                    "Split responsibilities or extract focused sub-modules.",
                    null));
            }
        }

        foreach (IGrouping<string, CouplingMetrics> group in couplings.GroupBy(coupling => coupling.Target))
        {
            int count = group.Select(coupling => coupling.Source).Distinct(StringComparer.Ordinal).Count();
            if (count > thresholds.MaxDependents)
            {
                issues.Add(new CouplingIssue(
                    IssueType.HighAfferentCoupling,
                    count > thresholds.MaxDependents * 2 ? Severity.High : Severity.Medium,
                    "",
                    group.Key,
                    1.0,
                    "Component has too many distinct dependents.",
                    "Stabilize the public API or split the shared model.",
                    null));
            }
        }
    }

    internal static void AddCircularDependencyIssues(IEnumerable<CouplingMetrics> couplings, List<CouplingIssue> issues)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        foreach (CouplingMetrics coupling in couplings)
        {
            string sourceNamespace = NamespaceOf(coupling.Source);
            string targetNamespace = NamespaceOf(coupling.Target);
            if (string.IsNullOrWhiteSpace(sourceNamespace) || sourceNamespace == targetNamespace)
            {
                continue;
            }

            if (!graph.TryGetValue(sourceNamespace, out HashSet<string>? targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                graph[sourceNamespace] = targets;
            }

            targets.Add(targetNamespace);
            graph.TryAdd(targetNamespace, new HashSet<string>(StringComparer.Ordinal));
        }

        foreach (IReadOnlyCollection<string> component in FindStronglyConnectedComponents(graph))
        {
            if (component.Count <= 1)
            {
                continue;
            }

            string source = string.Join(" -> ", component.Order(StringComparer.Ordinal));
            issues.Add(new CouplingIssue(
                IssueType.CircularDependency,
                Severity.High,
                source,
                source,
                0.0,
                "Namespaces form a circular dependency.",
                "Invert one direction via an interface, extract a shared contract, or use events.",
                null));
        }
    }

    internal static void AddHiddenCouplingIssues(
        IReadOnlyList<TemporalCoupling> temporalCouplings,
        IEnumerable<CouplingMetrics> couplings,
        IReadOnlyDictionary<string, Component> componentsById,
        List<CouplingIssue> issues)
    {
        HashSet<(string FileA, string FileB)> explicitFilePairs = new();
        foreach (CouplingMetrics coupling in couplings)
        {
            if (!componentsById.TryGetValue(coupling.Source, out Component? source)
                || !componentsById.TryGetValue(coupling.Target, out Component? target))
            {
                continue;
            }

            if (source.FilePath == target.FilePath)
            {
                continue;
            }

            explicitFilePairs.Add(OrderPair(source.FilePath, target.FilePath));
        }

        foreach (TemporalCoupling temporalCoupling in temporalCouplings)
        {
            (string FileA, string FileB) pair = OrderPair(temporalCoupling.FileA, temporalCoupling.FileB);
            if (explicitFilePairs.Contains(pair))
            {
                continue;
            }

            issues.Add(new CouplingIssue(
                IssueType.HiddenCoupling,
                temporalCoupling.CoChangeCount >= HiddenCouplingHighThreshold ? Severity.High : Severity.Medium,
                temporalCoupling.FileA,
                temporalCoupling.FileB,
                0.50,
                $"Files changed together {temporalCoupling.CoChangeCount} times without an explicit code dependency.",
                "Review whether a shared concept should be extracted, duplicated logic unified, or the dependency made explicit.",
                null));
        }
    }

    internal static void AddScatteredExternalCouplingIssues(
        IEnumerable<CouplingMetrics> couplings,
        List<CouplingIssue> issues,
        AnalysisThresholds? thresholds = null)
    {
        thresholds ??= AnalysisThresholds.Default;
        foreach (IGrouping<string, CouplingMetrics> group in couplings
            .Where(coupling => coupling.Distance == Distance.ExternalPackage)
            .GroupBy(coupling => coupling.Target, StringComparer.Ordinal))
        {
            int directUsers = group.Select(coupling => coupling.Source).Distinct(StringComparer.Ordinal).Count();
            if (directUsers < thresholds.ScatteredExternalBreadth)
            {
                continue;
            }

            issues.Add(new CouplingIssue(
                IssueType.ScatteredExternalCoupling,
                Severity.Medium,
                "",
                group.Key,
                0.50,
                $"External package namespace is used directly by {directUsers} internal components.",
                "Introduce a wrapper or adapter so upgrade risk is concentrated behind an internal API.",
                null));
        }
    }

    internal static (string FileA, string FileB) OrderPair(string first, string second)
    {
        return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
    }

    internal static string NamespaceOf(string componentId)
    {
        int lastDot = componentId.LastIndexOf('.');
        return lastDot < 0 ? "" : componentId[..lastDot];
    }

    private static IEnumerable<CouplingIssue> ApplyIgnores(IEnumerable<CouplingIssue> issues, AnalysisOptions options)
    {
        foreach (CouplingIssue issue in issues)
        {
            if (options.IgnoreIssueTypes.Contains(issue.Type)
                || MatchesAnyNamespace(issue.Source, options.IgnoreNamespaces)
                || MatchesAnyNamespace(issue.Target, options.IgnoreNamespaces)
                || MatchesAnyPath(issue.Source, options.IgnorePathPatterns)
                || MatchesAnyPath(issue.Target, options.IgnorePathPatterns)
                || issue.Location is not null && MatchesAnyPath(issue.Location.File, options.IgnorePathPatterns))
            {
                continue;
            }

            yield return issue;
        }
    }

    private static bool MatchesAnyNamespace(string value, IReadOnlyList<string> namespaces)
    {
        return namespaces.Any(namespaceName =>
            value == namespaceName
            || value.StartsWith(namespaceName + ".", StringComparison.Ordinal));
    }

    private static bool MatchesAnyPath(string value, IReadOnlyList<string> patterns)
    {
        return !string.IsNullOrWhiteSpace(value) && PathPatternMatcher.IsMatch(value, patterns);
    }

    private static List<IReadOnlyCollection<string>> FindStronglyConnectedComponents(Dictionary<string, HashSet<string>> graph)
    {
        int index = 0;
        Stack<string> stack = new();
        HashSet<string> onStack = new(StringComparer.Ordinal);
        Dictionary<string, int> indexes = new(StringComparer.Ordinal);
        Dictionary<string, int> lowLinks = new(StringComparer.Ordinal);
        List<IReadOnlyCollection<string>> components = [];

        foreach (string node in graph.Keys.Order(StringComparer.Ordinal))
        {
            if (!indexes.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        return components;

        void StrongConnect(string node)
        {
            indexes[node] = index;
            lowLinks[node] = index;
            index++;
            stack.Push(node);
            onStack.Add(node);

            foreach (string target in graph[node].Order(StringComparer.Ordinal))
            {
                if (!indexes.ContainsKey(target))
                {
                    StrongConnect(target);
                    lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                }
                else if (onStack.Contains(target) && indexes.TryGetValue(target, out int targetIndex))
                {
                    lowLinks[node] = Math.Min(lowLinks[node], targetIndex);
                }
            }

            if (lowLinks[node] != indexes[node])
            {
                return;
            }

            List<string> component = [];
            string current;
            do
            {
                current = stack.Pop();
                onStack.Remove(current);
                component.Add(current);
            }
            while (current != node);

            components.Add(component);
        }
    }

    private sealed record IssueIdentity(IssueType Type, string Source, string Target);
}
