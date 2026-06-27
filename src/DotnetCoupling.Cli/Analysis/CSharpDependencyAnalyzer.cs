namespace DotnetCoupling.Cli.Analysis;

public sealed class CSharpDependencyAnalyzer
{
    public static AnalysisReport Analyze(string targetPath, bool useGit, int gitMonths, AnalysisOptions? options = null)
    {
        options ??= AnalysisOptions.Default;
        string fullPath = Path.GetFullPath(targetPath);
        string[] files = FileDiscovery.DiscoverCSharpFiles(fullPath, options);
        List<Component> components = [];
        List<DependencyObservation> observations = [];
        Dictionary<string, List<UsingNamespace>> usingNamespacesByFile = new(StringComparer.Ordinal);

        foreach (string file in files)
        {
            SyntaxFileAnalysis syntaxFile = CSharpSyntaxDependencyCollector.AnalyzeFile(file);
            usingNamespacesByFile[file] = syntaxFile.UsingNamespaces.ToList();
            components.AddRange(syntaxFile.Components);
            observations.AddRange(syntaxFile.Observations);
        }

        Dictionary<string, Component> componentsById = components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        HashSet<string> internalNamespaces = components
            .Select(component => component.Namespace)
            .Where(namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> analyzedFiles = files.ToHashSet(StringComparer.Ordinal);

        IReadOnlyDictionary<string, int> changeCounts = useGit
            ? GitVolatility.GetChangeCounts(fullPath, gitMonths)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<TemporalCoupling> temporalCouplings = useGit
            ? GitVolatility.GetTemporalCouplings(
                fullPath,
                gitMonths,
                analyzedFiles,
                options.Thresholds.MinTemporalCoupling,
                options.Thresholds.MaxTemporalFilesPerCommit)
            : [];

        List<CouplingMetrics> couplings = CouplingResolver.Resolve(components, observations, changeCounts);
        ExternalCouplingDetector.AddExternalUsingCouplings(couplings, components, usingNamespacesByFile, internalNamespaces);

        List<BalanceScore> scores = couplings.Select(CouplingScoring.Calculate).ToList();
        int internalCouplingCount = couplings.Count(coupling => coupling.Distance != Distance.ExternalPackage);
        int externalCouplingCount = couplings.Count - internalCouplingCount;
        List<CouplingIssue> issues = IssueDetector.DetectIssues(scores, temporalCouplings, componentsById, options);
        GradeResult grade = CouplingScoring.CalculateGrade(internalCouplingCount, issues);
        AnalysisSummary summary = new(
            fullPath,
            "syntax-only",
            files.Length,
            components.Count,
            internalCouplingCount,
            externalCouplingCount,
            useGit,
            useGit && changeCounts.Count > 0,
            gitMonths);

        return new AnalysisReport(
            summary,
            grade,
            scores.Count == 0 ? 1.0 : scores.Average(score => score.Score),
            components,
            observations,
            couplings,
            issues,
            [
                "Semantic symbol resolution is not enabled.",
                "DI container runtime resolution is not analyzed in syntax-only mode.",
                "Reflection and dynamic calls may be incomplete.",
                "Generated code is excluded by default.",
            ]);
    }
}
