using DotnetCoupling.Core;

namespace DotnetCoupling.Cli;

internal static class CliPathResolver
{
    public static string ResolveAnalysisTargetPath(string fullTargetPath, AnalysisMode analysisMode)
    {
        if (analysisMode != AnalysisMode.Semantic || File.Exists(fullTargetPath))
        {
            return fullTargetPath;
        }

        string[] candidates = Directory
            .EnumerateFiles(fullTargetPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
                path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new NotSupportedException(
                "Semantic mode directory input requires one .slnx, .sln, or .csproj in the target directory. Pass a project or solution path explicitly."),
            _ => throw new NotSupportedException(CreateAmbiguousSemanticDirectoryMessage(candidates)),
        };
    }

    public static string? FindGitRepositoryRoot(string targetPath)
    {
        DirectoryInfo? directory = File.Exists(targetPath)
            ? new FileInfo(targetPath).Directory
            : new DirectoryInfo(targetPath);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string ResolveOutputRoot(string targetPath)
    {
        return Directory.Exists(targetPath)
            ? targetPath
            : Path.GetDirectoryName(targetPath) ?? targetPath;
    }

    private static string CreateAmbiguousSemanticDirectoryMessage(IReadOnlyList<string> candidates)
    {
        return "Semantic mode directory input is ambiguous. Pass one of these project or solution paths explicitly:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, candidates.Select(candidate => "- " + candidate));
    }
}
