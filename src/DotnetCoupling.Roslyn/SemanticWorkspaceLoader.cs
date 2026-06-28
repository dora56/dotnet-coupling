using DotnetCoupling.Core;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotnetCoupling.Roslyn;

internal static class SemanticWorkspaceLoader
{
    private static readonly Lock RegistrationLock = new();
    private static bool _registered;

    internal static SemanticWorkspaceLoadResult Load(string targetPath, AnalysisOptions options)
    {
        string fullPath = Path.GetFullPath(targetPath);
        if (!File.Exists(fullPath) || (!fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && !fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException("Semantic mode requires a .csproj or .sln input.");
        }

        EnsureMsBuildRegistered();

        using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        Solution solution = fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? workspace.OpenProjectAsync(fullPath).GetAwaiter().GetResult().Solution
            : workspace.OpenSolutionAsync(fullPath).GetAwaiter().GetResult();

        SemanticWorkspaceProject[] projects = solution.Projects
            .Select(project => new SemanticWorkspaceProject(
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

        AnalysisDiagnostic[] diagnostics = workspace.Diagnostics
            .Select(diagnostic => new AnalysisDiagnostic(
                $"workspace-{diagnostic.Kind.ToString().ToLowerInvariant()}",
                diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? "Warning" : "Info",
                diagnostic.Message,
                fullPath))
            .ToArray();

        return new SemanticWorkspaceLoadResult(projects, diagnostics);
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

internal sealed record SemanticWorkspaceLoadResult(
    IReadOnlyList<SemanticWorkspaceProject> Projects,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics);

internal sealed record SemanticWorkspaceProject(
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    IReadOnlyList<string> SourceFiles);
