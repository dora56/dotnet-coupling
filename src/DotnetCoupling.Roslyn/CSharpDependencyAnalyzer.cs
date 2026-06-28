using DotnetCoupling.Core;

namespace DotnetCoupling.Roslyn;

public sealed class CSharpDependencyAnalyzer
{
    public static AnalysisReport Analyze(string targetPath, bool useGit, int gitMonths, AnalysisOptions? options = null)
    {
        if (useGit)
        {
            throw new InvalidOperationException(
                "Git-backed analysis requires an injected volatility provider. Use the overload that accepts IVolatilityProvider.");
        }

        return Analyze(targetPath, volatilityProvider: null, gitMonths, options);
    }

    public static AnalysisReport Analyze(
        string targetPath,
        IVolatilityProvider? volatilityProvider,
        int gitMonths,
        AnalysisOptions? options = null)
    {
        options ??= AnalysisOptions.Default;
        string fullPath = Path.GetFullPath(targetPath);
        ProjectModel projectModel = ProjectModel.Load(fullPath, options);
        IReadOnlyList<ProjectFile> projectFiles = projectModel.Projects.Count == 0
            ? FileDiscovery.DiscoverCSharpFiles(fullPath, options)
                .Select(file => new ProjectFile(file, ProjectName: null))
                .ToArray()
            : projectModel.Projects
                .SelectMany(project => project.SourceFiles.Select(file => new ProjectFile(file, project.ProjectName)))
                .DistinctBy(projectFile => projectFile.FilePath)
                .OrderBy(projectFile => projectFile.FilePath, StringComparer.Ordinal)
                .ToArray();
        List<Component> components = [];
        List<DependencyObservation> observations = [];
        Dictionary<string, List<UsingNamespace>> usingNamespacesByFile = new(StringComparer.Ordinal);

        foreach (ProjectFile projectFile in projectFiles)
        {
            SyntaxFileAnalysis syntaxFile = CSharpSyntaxDependencyCollector.AnalyzeFile(projectFile.FilePath, projectFile.ProjectName);
            usingNamespacesByFile[projectFile.FilePath] = syntaxFile.UsingNamespaces.ToList();
            components.AddRange(syntaxFile.Components);
            observations.AddRange(syntaxFile.Observations);
        }

        components = CoalesceComponents(components);
        Dictionary<string, Component> componentsById = components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        HashSet<string> internalNamespaces = components
            .Select(component => component.Namespace)
            .Where(namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> analyzedFiles = projectFiles.Select(projectFile => projectFile.FilePath).ToHashSet(StringComparer.Ordinal);
        VolatilityAnalysis volatilityAnalysis = volatilityProvider?.Analyze(fullPath, gitMonths, analyzedFiles, options)
            ?? VolatilityAnalysis.Empty;

        List<CouplingMetrics> couplings = CouplingResolver.Resolve(components, observations, volatilityAnalysis.ChangeCounts);
        ExternalCouplingDetector.AddExternalUsingCouplings(couplings, components, usingNamespacesByFile, internalNamespaces);

        List<BalanceScore> scores = couplings.Select(CouplingScoring.Calculate).ToList();
        int internalCouplingCount = couplings.Count(coupling => coupling.Distance != Distance.ExternalPackage);
        int externalCouplingCount = couplings.Count - internalCouplingCount;
        List<CouplingIssue> issues = IssueDetector.DetectIssues(scores, volatilityAnalysis.TemporalCouplings, componentsById, options);
        GradeResult grade = CouplingScoring.CalculateGrade(internalCouplingCount, issues);
        AnalysisSummary summary = new(
            fullPath,
            "syntax-only",
            projectFiles.Count,
            components.Count,
            internalCouplingCount,
            externalCouplingCount,
            volatilityProvider is not null,
            volatilityProvider is not null && volatilityAnalysis.ChangeCounts.Count > 0,
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
            ],
            Diagnostics: projectModel.Diagnostics
                .Select(diagnostic => new AnalysisDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Path))
                .ToArray());
    }

    private sealed record ProjectFile(string FilePath, string? ProjectName);

    private static List<Component> CoalesceComponents(IEnumerable<Component> components)
    {
        return components
            .GroupBy(component => component.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }
}
