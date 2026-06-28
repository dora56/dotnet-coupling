using System.Xml;
using System.Xml.Linq;
using DotnetCoupling.Core;

namespace DotnetCoupling.Roslyn;

internal sealed record ProjectModel(
    IReadOnlyList<ProjectModelProject> Projects,
    IReadOnlyList<ProjectModelDiagnostic> Diagnostics)
{
    internal static ProjectModel Load(string fullPath, AnalysisOptions options)
    {
        if (File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new ProjectModel([LoadProject(fullPath, options)], []);
        }

        if (File.Exists(fullPath) && fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return LoadSlnx(fullPath, options);
        }

        if (File.Exists(fullPath) && fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return LoadSln(fullPath, options);
        }

        return new ProjectModel([], []);
    }

    private static ProjectModel LoadSlnx(string solutionPath, AnalysisOptions options)
    {
        XDocument document = XDocument.Load(solutionPath);
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        List<ProjectModelProject> projects = [];
        List<ProjectModelDiagnostic> diagnostics = [];
        foreach (XElement projectElement in document.Descendants().Where(element => element.Name.LocalName == "Project"))
        {
            string? path = projectElement.Attribute("Path")?.Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string projectPath = Path.GetFullPath(Path.Combine(solutionDirectory, path));
            if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                TryLoadProject(projectPath, options, projects, diagnostics);
            }
            else
            {
                diagnostics.Add(new ProjectModelDiagnostic(
                    "missing-project",
                    "Warning",
                    $"Project listed in solution was not found: {projectPath}",
                    solutionPath));
            }
        }

        return BuildModel(projects, diagnostics);
    }

    private static ProjectModel LoadSln(string solutionPath, AnalysisOptions options)
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        List<ProjectModelProject> projects = [];
        List<ProjectModelDiagnostic> diagnostics = [];
        foreach (string line in File.ReadLines(solutionPath))
        {
            string[] parts = line.Split('"');
            string? projectPath = parts.FirstOrDefault(part => part.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                continue;
            }

            string normalizedPath = projectPath.Replace('\\', Path.DirectorySeparatorChar);
            string fullProjectPath = Path.GetFullPath(Path.Combine(solutionDirectory, normalizedPath));
            if (File.Exists(fullProjectPath))
            {
                TryLoadProject(fullProjectPath, options, projects, diagnostics);
            }
            else
            {
                diagnostics.Add(new ProjectModelDiagnostic(
                    "missing-project",
                    "Warning",
                    $"Project listed in solution was not found: {fullProjectPath}",
                    solutionPath));
            }
        }

        return BuildModel(projects, diagnostics);
    }

    private static ProjectModelProject LoadProject(string projectPath, AnalysisOptions options)
    {
        XDocument document = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        string? assemblyName = ReadProperty(document, "AssemblyName");
        string projectName = assemblyName
            ?? ReadProperty(document, "RootNamespace")
            ?? Path.GetFileNameWithoutExtension(projectPath);

        string[] sourceFiles = FileDiscovery.DiscoverCSharpFiles(projectDirectory, options);
        string[] projectReferencePaths = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!.Replace('\\', Path.DirectorySeparatorChar))))
            .ToArray();
        ProjectModelPackageReference[] packageReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => new ProjectModelPackageReference(
                element.Attribute("Include")?.Value ?? string.Empty,
                element.Attribute("Version")?.Value))
            .Where(reference => !string.IsNullOrWhiteSpace(reference.PackageId))
            .ToArray();
        return new ProjectModelProject(
            projectPath,
            projectName,
            assemblyName ?? projectName,
            projectDirectory,
            sourceFiles,
            projectReferencePaths,
            [],
            packageReferences);
    }

    private static void TryLoadProject(
        string projectPath,
        AnalysisOptions options,
        List<ProjectModelProject> projects,
        List<ProjectModelDiagnostic> diagnostics)
    {
        try
        {
            projects.Add(LoadProject(projectPath, options));
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ProjectModelDiagnostic(
                "invalid-project",
                "Warning",
                $"Project could not be loaded and was skipped: {projectPath}",
                projectPath));
        }
    }

    private static ProjectModel BuildModel(
        IReadOnlyList<ProjectModelProject> projects,
        IReadOnlyList<ProjectModelDiagnostic> loadDiagnostics)
    {
        List<ProjectModelDiagnostic> diagnostics = [.. loadDiagnostics];
        ProjectModelProject[] resolvedProjects = ResolveProjectReferences(projects, diagnostics);
        return new ProjectModel(resolvedProjects, diagnostics);
    }

    private static ProjectModelProject[] ResolveProjectReferences(
        IReadOnlyList<ProjectModelProject> projects,
        List<ProjectModelDiagnostic> diagnostics)
    {
        Dictionary<string, ProjectModelProject> projectsByPath = projects.ToDictionary(
            project => project.ProjectPath,
            StringComparer.Ordinal);

        return projects
            .Select(project => project with
            {
                ProjectReferences = project.ProjectReferencePaths
                    .Select(referencePath =>
                    {
                        if (!projectsByPath.TryGetValue(referencePath, out ProjectModelProject? targetProject))
                        {
                            diagnostics.Add(new ProjectModelDiagnostic(
                                "missing-project-reference",
                                "Warning",
                                $"Referenced project was not found: {referencePath}",
                                project.ProjectPath));
                            return null;
                        }

                        return new ProjectModelReference(referencePath, targetProject.ProjectName);
                    })
                    .Where(reference => reference is not null)
                    .Select(reference => reference!)
                    .ToArray(),
            })
            .ToArray();
    }

    private static string? ReadProperty(XDocument document, string name)
    {
        return document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == name)
            ?.Value
            .Trim();
    }
}

internal sealed record ProjectModelProject(
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    string ProjectDirectory,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> ProjectReferencePaths,
    IReadOnlyList<ProjectModelReference> ProjectReferences,
    IReadOnlyList<ProjectModelPackageReference> PackageReferences);

internal sealed record ProjectModelReference(
    string TargetProjectPath,
    string TargetProjectName);

internal sealed record ProjectModelPackageReference(
    string PackageId,
    string? Version);

internal sealed record ProjectModelDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path);
