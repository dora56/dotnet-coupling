using System.Xml.Linq;
using DotnetCoupling.Core;

namespace DotnetCoupling.Roslyn;

internal sealed record ProjectModel(IReadOnlyList<ProjectModelProject> Projects)
{
    internal static ProjectModel Load(string fullPath, AnalysisOptions options)
    {
        if (File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new ProjectModel([LoadProject(fullPath, options)]);
        }

        if (File.Exists(fullPath) && fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return LoadSlnx(fullPath, options);
        }

        if (File.Exists(fullPath) && fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return LoadSln(fullPath, options);
        }

        return new ProjectModel([]);
    }

    private static ProjectModel LoadSlnx(string solutionPath, AnalysisOptions options)
    {
        XDocument document = XDocument.Load(solutionPath);
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        List<ProjectModelProject> projects = [];
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
                projects.Add(LoadProject(projectPath, options));
            }
        }

        return new ProjectModel(projects);
    }

    private static ProjectModel LoadSln(string solutionPath, AnalysisOptions options)
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        List<ProjectModelProject> projects = [];
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
                projects.Add(LoadProject(fullProjectPath, options));
            }
        }

        return new ProjectModel(projects);
    }

    private static ProjectModelProject LoadProject(string projectPath, AnalysisOptions options)
    {
        XDocument document = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        string projectName = ReadProperty(document, "AssemblyName")
            ?? ReadProperty(document, "RootNamespace")
            ?? Path.GetFileNameWithoutExtension(projectPath);

        string[] sourceFiles = FileDiscovery.DiscoverCSharpFiles(projectDirectory, options);
        return new ProjectModelProject(projectPath, projectName, projectDirectory, sourceFiles);
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
    string ProjectDirectory,
    IReadOnlyList<string> SourceFiles);
