using DotnetCoupling.Core;
using System.Diagnostics;

namespace DotnetCoupling.Git;

public static class GitVolatility
{
    private const string CommitMarkerPrefix = "COMMIT:";

    public static IReadOnlyDictionary<string, int> GetChangeCounts(string repositoryPath, int months)
    {
        try
        {
            string repositoryRoot = GetRepositoryRoot(repositoryPath);
            ProcessStartInfo startInfo = new()
            {
                FileName = "git",
                WorkingDirectory = repositoryPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.ArgumentList.Add("log");
            startInfo.ArgumentList.Add("--pretty=format:");
            startInfo.ArgumentList.Add("--name-only");
            startInfo.ArgumentList.Add("--diff-filter=AMRC");
            startInfo.ArgumentList.Add($"--since={months} months ago");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("*.cs");

            using Process process = Process.Start(startInfo)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            return AnalyzeChangeCountsFromLog(repositoryRoot, output);
        }
        catch
        {
            /* git not available or repository unavailable; skip volatility analysis */
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }

    internal static IReadOnlyDictionary<string, int> AnalyzeChangeCountsFromLog(string repositoryPath, string gitLogOutput)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (string line in gitLogOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string fullPath = Path.GetFullPath(Path.Combine(repositoryPath, line));
            counts[fullPath] = counts.GetValueOrDefault(fullPath) + 1;
        }

        return counts;
    }

    public static IReadOnlyList<TemporalCoupling> GetTemporalCouplings(
        string repositoryPath,
        int months,
        IReadOnlySet<string> analyzedFiles,
        int minTemporalCoupling = 3,
        int maxTemporalFilesPerCommit = 50)
    {
        try
        {
            string repositoryRoot = GetRepositoryRoot(repositoryPath);
            ProcessStartInfo startInfo = new()
            {
                FileName = "git",
                WorkingDirectory = repositoryPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.ArgumentList.Add("log");
            startInfo.ArgumentList.Add("--no-merges");
            startInfo.ArgumentList.Add($"--pretty=format:{CommitMarkerPrefix}%H");
            startInfo.ArgumentList.Add("--name-only");
            startInfo.ArgumentList.Add("--diff-filter=AMRC");
            startInfo.ArgumentList.Add($"--since={months} months ago");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("*.cs");

            using Process process = Process.Start(startInfo)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return [];
            }

            return AnalyzeTemporalCouplingsFromLog(
                repositoryRoot,
                output,
                analyzedFiles,
                minTemporalCoupling,
                maxTemporalFilesPerCommit);
        }
        catch
        {
            return [];
        }
    }

    public static IReadOnlyList<TemporalCoupling> AnalyzeTemporalCouplingsFromLog(
        string repositoryPath,
        string gitLogOutput,
        IReadOnlySet<string> analyzedFiles,
        int minTemporalCoupling = 3,
        int maxTemporalFilesPerCommit = 50)
    {
        Dictionary<(string FileA, string FileB), int> counts = new();
        HashSet<string> currentCommitFiles = new(StringComparer.Ordinal);

        foreach (string rawLine in gitLogOutput.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (rawLine.StartsWith(CommitMarkerPrefix, StringComparison.Ordinal))
            {
                AddPairs(currentCommitFiles);
                currentCommitFiles.Clear();
                continue;
            }

            string fullPath = Path.GetFullPath(Path.Combine(repositoryPath, rawLine));
            if (analyzedFiles.Contains(fullPath))
            {
                currentCommitFiles.Add(fullPath);
            }
        }

        AddPairs(currentCommitFiles);

        return counts
            .Where(pair => pair.Value >= minTemporalCoupling)
            .Select(pair => new TemporalCoupling(pair.Key.FileA, pair.Key.FileB, pair.Value))
            .OrderByDescending(coupling => coupling.CoChangeCount)
            .ThenBy(coupling => coupling.FileA, StringComparer.Ordinal)
            .ThenBy(coupling => coupling.FileB, StringComparer.Ordinal)
            .ToArray();

        void AddPairs(HashSet<string> files)
        {
            if (files.Count < 2 || files.Count > maxTemporalFilesPerCommit)
            {
                return;
            }

            string[] orderedFiles = files.Order(StringComparer.Ordinal).ToArray();
            for (int i = 0; i < orderedFiles.Length; i++)
            {
                for (int j = i + 1; j < orderedFiles.Length; j++)
                {
                    (string FileA, string FileB) key = (orderedFiles[i], orderedFiles[j]);
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }
        }
    }

    private static string GetRepositoryRoot(string repositoryPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--show-prefix");

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return repositoryPath;
        }

        string prefix = output.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Path.GetFullPath(repositoryPath);
        }

        DirectoryInfo? directory = new(Path.GetFullPath(repositoryPath));
        foreach (string _ in prefix.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            directory = directory.Parent;
            if (directory is null)
            {
                return repositoryPath;
            }
        }

        return directory.FullName;
    }
}
