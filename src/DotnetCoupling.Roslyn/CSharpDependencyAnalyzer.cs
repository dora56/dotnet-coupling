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

        return Analyze(targetPath, AnalysisMode.Syntax, volatilityProvider: null, gitMonths, options);
    }

    public static AnalysisReport Analyze(
        string targetPath,
        IVolatilityProvider? volatilityProvider,
        int gitMonths,
        AnalysisOptions? options = null)
    {
        return Analyze(targetPath, AnalysisMode.Syntax, volatilityProvider, gitMonths, options);
    }

    public static AnalysisReport Analyze(
        string targetPath,
        AnalysisMode mode,
        IVolatilityProvider? volatilityProvider,
        int gitMonths,
        AnalysisOptions? options = null)
    {
        options ??= AnalysisOptions.Default;
        string fullPath = Path.GetFullPath(targetPath);
        IReadOnlyList<ProjectFile> projectFiles;
        IReadOnlyList<AnalysisDiagnostic> diagnostics;
        string analysisModeLabel;
        ProjectMetadata? projectMetadata = null;
        List<Component> components = [];
        List<DependencyObservation> observations = [];
        Dictionary<string, List<UsingNamespace>> usingNamespacesByFile = new(StringComparer.Ordinal);

        if (mode == AnalysisMode.Semantic)
        {
            using SemanticWorkspaceSession workspace = SemanticWorkspaceLoader.Load(fullPath, options);
            projectFiles = workspace.Projects
                .SelectMany(project => project.SourceFiles.Select(file => new ProjectFile(file, project.ProjectName)))
                .DistinctBy(projectFile => projectFile.FilePath)
                .OrderBy(projectFile => projectFile.FilePath, StringComparer.Ordinal)
                .ToArray();
            diagnostics = workspace.Diagnostics;
            analysisModeLabel = "semantic-preview";
            projectMetadata = CreateProjectMetadata(workspace.Projects, options);

            foreach (SemanticWorkspaceProject project in workspace.Projects)
            {
                HashSet<string> projectSourceFiles = project.SourceFiles.ToHashSet(StringComparer.Ordinal);
                foreach (Microsoft.CodeAnalysis.Document document in project.RoslynProject.Documents.Where(document =>
                             document.SourceCodeKind == Microsoft.CodeAnalysis.SourceCodeKind.Regular
                             && document.FilePath is not null
                             && projectSourceFiles.Contains(document.FilePath)))
                {
                    SyntaxFileAnalysis syntaxFile = CSharpSyntaxDependencyCollector.AnalyzeDocument(document, project.ProjectName);
                    usingNamespacesByFile[document.FilePath!] = syntaxFile.UsingNamespaces.ToList();
                    components.AddRange(syntaxFile.Components);
                    observations.AddRange(syntaxFile.Observations);
                }
            }
        }
        else
        {
            ProjectModel projectModel = ProjectModel.Load(fullPath, options);
            string syntaxFallbackPath = File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(fullPath) ?? fullPath
                : fullPath;
            projectFiles = projectModel.Projects.Count == 0
                ? FileDiscovery.DiscoverCSharpFiles(syntaxFallbackPath, options)
                    .Select(file => new ProjectFile(file, ProjectName: null))
                    .ToArray()
                : projectModel.Projects
                    .SelectMany(project => project.SourceFiles.Select(file => new ProjectFile(file, project.ProjectName)))
                    .DistinctBy(projectFile => projectFile.FilePath)
                    .OrderBy(projectFile => projectFile.FilePath, StringComparer.Ordinal)
                    .ToArray();
            diagnostics = projectModel.Diagnostics
                .Select(diagnostic => new AnalysisDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Path))
                .ToArray();
            analysisModeLabel = "syntax-only";
            projectMetadata = CreateProjectMetadata(projectModel);

            foreach (ProjectFile projectFile in projectFiles)
            {
                SyntaxFileAnalysis syntaxFile = CSharpSyntaxDependencyCollector.AnalyzeFile(projectFile.FilePath, projectFile.ProjectName);
                usingNamespacesByFile[projectFile.FilePath] = syntaxFile.UsingNamespaces.ToList();
                components.AddRange(syntaxFile.Components);
                observations.AddRange(syntaxFile.Observations);
            }
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
            analysisModeLabel,
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
            CreateBlindSpots(mode),
            Diagnostics: diagnostics,
            ProjectMetadata: projectMetadata);
    }

    private sealed record ProjectFile(string FilePath, string? ProjectName);

    private static List<Component> CoalesceComponents(IEnumerable<Component> components)
    {
        return components
            .GroupBy(component => component.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<string> CreateBlindSpots(AnalysisMode mode)
    {
        if (mode == AnalysisMode.Semantic)
        {
            return
            [
                "Semantic preview resolves many symbol-aware dependencies, but some flows remain unresolved.",
                "DI container runtime resolution is not analyzed.",
                "Reflection and dynamic calls may be incomplete.",
                "Generated code is excluded by default.",
            ];
        }

        return
        [
            "Semantic symbol resolution is not enabled.",
            "DI container runtime resolution is not analyzed in syntax-only mode.",
            "Reflection and dynamic calls may be incomplete.",
            "Generated code is excluded by default.",
        ];
    }

    private static ProjectMetadata? CreateProjectMetadata(ProjectModel projectModel)
    {
        if (projectModel.Projects.Count == 0)
        {
            return null;
        }

        return new ProjectMetadata(
            projectModel.Projects.Count,
            projectModel.Projects
                .OrderBy(project => project.ProjectName, StringComparer.Ordinal)
                .Select(project => new ProjectMetadataEntry(
                    project.ProjectPath,
                    project.ProjectName,
                    project.AssemblyName,
                    project.SourceFiles.Count,
                    project.ProjectReferences.Select(reference => reference.TargetProjectName).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                    project.PackageReferences.Select(reference => reference.PackageId).OrderBy(name => name, StringComparer.Ordinal).ToArray()))
                .ToArray());
    }

    private static ProjectMetadata? CreateProjectMetadata(
        IReadOnlyList<SemanticWorkspaceProject> projects,
        AnalysisOptions options)
    {
        if (projects.Count == 0)
        {
            return null;
        }

        return new ProjectMetadata(
            projects.Count,
            projects
                .OrderBy(project => project.ProjectName, StringComparer.Ordinal)
                .Select(project =>
                {
                    ProjectModelProject? syntaxProject = File.Exists(project.ProjectPath)
                        ? ProjectModel.Load(project.ProjectPath, options).Projects.SingleOrDefault()
                        : null;
                    return new ProjectMetadataEntry(
                        project.ProjectPath,
                        project.ProjectName,
                        project.AssemblyName,
                        project.SourceFiles.Count,
                        project.RoslynProject.ProjectReferences
                            .Select(reference => project.RoslynProject.Solution.GetProject(reference.ProjectId))
                            .Where(targetProject => targetProject is not null)
                            .Select(targetProject => targetProject!.AssemblyName ?? targetProject.Name)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToArray(),
                        syntaxProject?.PackageReferences
                            .Select(reference => reference.PackageId)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToArray()
                        ?? []);
                })
                .ToArray());
    }
}
