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
        return DetectIssuesWithSuppression(scores, temporalCouplings, componentsById, options).Issues;
    }

    internal static IssueDetectionResult DetectIssuesWithSuppression(
        IReadOnlyCollection<BalanceScore> scores,
        IReadOnlyList<TemporalCoupling> temporalCouplings,
        IReadOnlyDictionary<string, Component> componentsById,
        AnalysisOptions? options = null,
        IReadOnlyCollection<CouplingMetrics>? observedCouplings = null)
    {
        options ??= AnalysisOptions.Default;
        List<CouplingIssue> issues = [];
        List<BalanceScore> issueScores = scores
            .Where(score => !IsTestProjectFile(score.Coupling.Location.File, options.TestProjectPathPatterns))
            .ToList();
        List<CouplingMetrics> observedIssueCouplings = (observedCouplings ?? issueScores.Select(score => score.Coupling).ToArray())
            .Where(coupling => !IsTestProjectFile(coupling.Location.File, options.TestProjectPathPatterns))
            .ToList();
        List<TemporalCoupling> issueTemporalCouplings = temporalCouplings
            .Where(temporalCoupling =>
                !IsTestProjectFile(temporalCoupling.FileA, options.TestProjectPathPatterns)
                && !IsTestProjectFile(temporalCoupling.FileB, options.TestProjectPathPatterns))
            .ToList();

        foreach (BalanceScore score in issueScores)
        {
            CouplingMetrics coupling = score.Coupling;
            if (coupling.Strength >= IntegrationStrength.Functional && coupling.Distance >= Distance.DifferentNamespace)
            {
                issues.Add(new CouplingIssue(
                    IssueType.GlobalComplexity,
                    DetermineGlobalComplexitySeverity(score, componentsById, options.DomainContext),
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Strong coupling spans a namespace or project boundary.",
                    "Introduce an interface, move the dependency closer, or add a port/adapter.",
                    coupling.Location));
            }

            if (coupling.Strength >= IntegrationStrength.Functional
                && coupling.Distance >= Distance.DifferentNamespace
                && coupling.Volatility == Volatility.High)
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

        IEnumerable<CouplingMetrics> issueCouplings = issueScores.Select(score => score.Coupling);
        AddFanInFanOutIssues(issueCouplings, issues, options.Thresholds);
        AddCircularDependencyIssues(issueCouplings, componentsById, issues);
        AddHiddenCouplingIssues(issueTemporalCouplings, observedIssueCouplings, componentsById, issues);
        AddScatteredExternalCouplingIssues(issueCouplings, issues, options.Thresholds);
        AddAccidentalVolatilityIssues(observedIssueCouplings, componentsById, issues, options.DomainContext);
        List<CouplingIssue> deduplicatedIssues = ApplyIgnores(issues, options)
            .GroupBy(issue => new IssueIdentity(issue.Type, issue.Source, issue.Target))
            .Select(group => group
                .OrderBy(issue => issue.Location?.File ?? "", StringComparer.Ordinal)
                .ThenBy(issue => issue.Location?.Line ?? 0)
                .First())
            .ToList();

        IssueDetectionResult result = ApplySuppressions(deduplicatedIssues, options.IssueSuppressions);
        return result with
        {
            DomainContext = CreateDomainContextSummary(
                options.DomainContext,
                componentsById.Values,
                options.TestProjectPathPatterns,
                result.Issues),
            ComponentRoles = CreateComponentRoleContexts(
                options.DomainContext,
                componentsById.Values,
                options.TestProjectPathPatterns),
        };
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

    internal static void AddCircularDependencyIssues(
        IEnumerable<CouplingMetrics> couplings,
        IReadOnlyDictionary<string, Component> componentsById,
        List<CouplingIssue> issues)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        foreach (CouplingMetrics coupling in couplings)
        {
            if (!componentsById.TryGetValue(coupling.Source, out Component? source)
                || !componentsById.TryGetValue(coupling.Target, out Component? target))
            {
                continue;
            }

            string sourceNamespace = source.Namespace;
            string targetNamespace = target.Namespace;
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

    private static Severity DetermineGlobalComplexitySeverity(
        BalanceScore score,
        IReadOnlyDictionary<string, Component> componentsById,
        DomainContext domainContext)
    {
        if (score.Score >= 0.40)
        {
            return Severity.Medium;
        }

        if (!componentsById.TryGetValue(score.Coupling.Target, out Component? target))
        {
            return Severity.High;
        }

        DomainArea? targetArea = DomainContextMatcher.FindArea(target.FilePath, domainContext);
        if (targetArea?.TechnicalRole == TechnicalRole.DomainModel)
        {
            return Severity.High;
        }

        DomainSubdomain? targetSubdomain = DomainContextMatcher.FindSubdomain(target.FilePath, domainContext);
        if (targetSubdomain is null)
        {
            return Severity.High;
        }

        return targetSubdomain.ExpectedVolatility == Volatility.High ? Severity.High : Severity.Medium;
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

    internal static void AddAccidentalVolatilityIssues(
        IEnumerable<CouplingMetrics> couplings,
        IReadOnlyDictionary<string, Component> componentsById,
        List<CouplingIssue> issues,
        DomainContext domainContext)
    {
        if (domainContext.Subdomains.Count == 0)
        {
            return;
        }

        foreach (CouplingMetrics coupling in couplings
            .Where(coupling => coupling.Volatility == Volatility.High)
            .DistinctBy(coupling => coupling.Target))
        {
            if (!componentsById.TryGetValue(coupling.Target, out Component? target))
            {
                continue;
            }

            DomainSubdomain? subdomain = DomainContextMatcher.FindSubdomain(target.FilePath, domainContext);
            if (subdomain is null
                || subdomain.Category == SubdomainCategory.Core
                || subdomain.ExpectedVolatility == Volatility.High)
            {
                continue;
            }

            issues.Add(new CouplingIssue(
                IssueType.AccidentalVolatility,
                Severity.Medium,
                target.Id,
                subdomain.Name,
                0.50,
                $"{subdomain.Category} subdomain '{subdomain.Name}' has High observed churn but expected {subdomain.ExpectedVolatility} volatility.",
                "Review whether the churn is business-driven; if not, stabilize the boundary or remove design and implementation friction.",
                new SourceLocation(target.FilePath, 1)));
        }
    }

    internal static (string FileA, string FileB) OrderPair(string first, string second)
    {
        return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
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

    private static IssueDetectionResult ApplySuppressions(
        IEnumerable<CouplingIssue> issues,
        IReadOnlyList<IssueSuppression> suppressions)
    {
        if (suppressions.Count == 0)
        {
            return new IssueDetectionResult(issues.ToList(), []);
        }

        Dictionary<IssueIdentity, IssueSuppression> suppressionsByIdentity = suppressions
            .GroupBy(suppression => new IssueIdentity(suppression.Type, suppression.Source, suppression.Target))
            .ToDictionary(group => group.Key, group => group.First());
        List<CouplingIssue> activeIssues = [];
        List<SuppressedIssue> suppressedIssues = [];

        foreach (CouplingIssue issue in issues)
        {
            IssueIdentity identity = new(issue.Type, issue.Source, issue.Target);
            if (suppressionsByIdentity.TryGetValue(identity, out IssueSuppression? suppression))
            {
                suppressedIssues.Add(new SuppressedIssue(issue, suppression.Reason));
                continue;
            }

            activeIssues.Add(issue);
        }

        return new IssueDetectionResult(activeIssues, suppressedIssues);
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

    private static bool IsTestProjectFile(string filePath, IReadOnlyList<string> testProjectPathPatterns)
    {
        return testProjectPathPatterns.Count > 0
            && !string.IsNullOrWhiteSpace(filePath)
            && PathPatternMatcher.IsMatch(filePath, testProjectPathPatterns);
    }

    private static DomainContextSummary? CreateDomainContextSummary(
        DomainContext domainContext,
        IEnumerable<Component> components,
        IReadOnlyList<string> testProjectPathPatterns,
        IReadOnlyCollection<CouplingIssue> activeIssues)
    {
        if (domainContext.Subdomains.Count == 0 && domainContext.Areas.Count == 0)
        {
            return null;
        }

        Dictionary<string, int> matchedCountsByName = domainContext.Subdomains.ToDictionary(
            subdomain => subdomain.Name,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> matchedAreaCountsByName = domainContext.Areas.ToDictionary(
            area => area.Name,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);
        int matchedComponents = 0;
        int unmatchedComponents = 0;
        int matchedAreaComponents = 0;
        int unmatchedAreaComponents = 0;

        foreach (Component component in components.Where(component =>
            component.Kind != ComponentKind.ExternalPackage
            && !IsTestProjectFile(component.FilePath, testProjectPathPatterns)))
        {
            if (domainContext.Subdomains.Count > 0)
            {
                DomainSubdomain? subdomain = DomainContextMatcher.FindSubdomain(component.FilePath, domainContext);
                if (subdomain is null)
                {
                    unmatchedComponents++;
                }
                else
                {
                    matchedComponents++;
                    matchedCountsByName[subdomain.Name]++;
                }
            }

            if (domainContext.Areas.Count > 0)
            {
                DomainArea? area = DomainContextMatcher.FindArea(component.FilePath, domainContext);
                if (area is null)
                {
                    unmatchedAreaComponents++;
                }
                else
                {
                    matchedAreaComponents++;
                    matchedAreaCountsByName[area.Name]++;
                }
            }
        }

        DomainSubdomainUsage[] subdomains = domainContext.Subdomains
            .Select(subdomain => new DomainSubdomainUsage(
                subdomain.Name,
                subdomain.Category,
                subdomain.ExpectedVolatility,
                matchedCountsByName[subdomain.Name],
                subdomain.StrategicRole))
            .ToArray();

        DomainAreaUsage[] areas = domainContext.Areas
            .Select(area => new DomainAreaUsage(
                area.Name,
                area.TechnicalRole,
                matchedAreaCountsByName[area.Name]))
            .ToArray();

        return new DomainContextSummary(
            domainContext.Subdomains.Count,
            matchedComponents,
            unmatchedComponents,
            activeIssues.Count(issue => issue.Type == IssueType.AccidentalVolatility),
            subdomains,
            domainContext.Areas.Count,
            matchedAreaComponents,
            unmatchedAreaComponents,
            areas);
    }

    private static List<ComponentRoleContext>? CreateComponentRoleContexts(
        DomainContext domainContext,
        IEnumerable<Component> components,
        IReadOnlyList<string> testProjectPathPatterns)
    {
        if (domainContext.Subdomains.Count == 0 && domainContext.Areas.Count == 0)
        {
            return null;
        }

        List<ComponentRoleContext> contexts = [];
        foreach (Component component in components.Where(component =>
            component.Kind != ComponentKind.ExternalPackage
            && !IsTestProjectFile(component.FilePath, testProjectPathPatterns)))
        {
            DomainSubdomain? subdomain = DomainContextMatcher.FindSubdomain(component.FilePath, domainContext);
            DomainArea? area = DomainContextMatcher.FindArea(component.FilePath, domainContext);
            if (subdomain is null && area is null)
            {
                continue;
            }

            contexts.Add(new ComponentRoleContext(
                component.Id,
                component.FilePath,
                subdomain?.Name,
                subdomain?.StrategicRole,
                area?.Name,
                area?.TechnicalRole));
        }

        return contexts.Count == 0 ? null : contexts;
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

internal sealed record IssueDetectionResult(
    List<CouplingIssue> Issues,
    List<SuppressedIssue> SuppressedIssues,
    DomainContextSummary? DomainContext = null,
    IReadOnlyList<ComponentRoleContext>? ComponentRoles = null);
