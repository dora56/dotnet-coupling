using DotnetCoupling.Core;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotnetCoupling.Roslyn;

internal static class SemanticWorkspaceLoader
{
    private static readonly Lock RegistrationLock = new();
    private static bool _registered;

    internal static SemanticWorkspaceSession Load(string targetPath, AnalysisOptions options)
    {
        string fullPath = Path.GetFullPath(targetPath);
        if (!File.Exists(fullPath)
            || (!fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                && !fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                && !fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException("Semantic mode requires a .csproj, .sln, or .slnx input.");
        }

        EnsureMsBuildRegistered();

        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        List<AnalysisDiagnostic> diagnostics = [];
        Solution solution;

        if (fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            solution = workspace.OpenProjectAsync(fullPath).GetAwaiter().GetResult().Solution;
        }
        else if (fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            solution = workspace.OpenSolutionAsync(fullPath).GetAwaiter().GetResult();
        }
        else
        {
            ProjectModel projectModel = ProjectModel.Load(fullPath, options);
            diagnostics.AddRange(projectModel.Diagnostics.Select(diagnostic => new AnalysisDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.Path)));

            HashSet<string> loadedProjectPaths = new(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectModelProject project in projectModel.Projects)
            {
                if (!loadedProjectPaths.Add(project.ProjectPath))
                {
                    continue;
                }

                Project loadedProject = workspace.OpenProjectAsync(project.ProjectPath).GetAwaiter().GetResult();
                foreach (Project solutionProject in loadedProject.Solution.Projects)
                {
                    if (solutionProject.FilePath is not null)
                    {
                        loadedProjectPaths.Add(solutionProject.FilePath);
                    }
                }
            }

            solution = workspace.CurrentSolution;
        }

        SemanticWorkspaceProject[] projects = solution.Projects
            .Select(project => new SemanticWorkspaceProject(
                project,
                project.FilePath ?? project.Name,
                project.Name,
                project.AssemblyName ?? project.Name,
                project.Documents
                    .Where(document => document.SourceCodeKind == SourceCodeKind.Regular && document.FilePath is not null)
                    .Select(document => document.FilePath!)
                    .Where(filePath => IsAnalyzableSource(filePath, options))
                    .Where(filePath => filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(filePath => filePath, StringComparer.Ordinal)
                    .ToArray()))
            .Where(project => project.SourceFiles.Count > 0)
            .ToArray();

        diagnostics.AddRange(workspace.Diagnostics.Select(diagnostic => new AnalysisDiagnostic(
                $"workspace-{diagnostic.Kind.ToString().ToLowerInvariant()}",
                diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? "Warning" : "Info",
                diagnostic.Message,
                fullPath)));

        return new SemanticWorkspaceSession(workspace, projects, diagnostics);
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (RegistrationLock)
        {
            if (_registered || MSBuildLocator.IsRegistered)
            {
                _registered = true;
                return;
            }

            MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }

    private static bool IsAnalyzableSource(string filePath, AnalysisOptions options)
    {
        return !FileDiscovery.IsExcludedPath(filePath)
            && !FileDiscovery.IsGeneratedFile(filePath)
            && !PathPatternMatcher.IsMatch(filePath, options.ExcludePathPatterns);
    }
}

internal sealed class SemanticWorkspaceSession(
    MSBuildWorkspace workspace,
    IReadOnlyList<SemanticWorkspaceProject> projects,
    IReadOnlyList<AnalysisDiagnostic> diagnostics) : IDisposable
{
    internal IReadOnlyList<SemanticWorkspaceProject> Projects { get; } = projects;

    internal IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; } = diagnostics;

    public void Dispose()
    {
        workspace.Dispose();
    }
}

internal sealed record SemanticWorkspaceProject(
    Project RoslynProject,
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    IReadOnlyList<string> SourceFiles);
