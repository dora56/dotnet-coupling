using System.Diagnostics;

namespace DotnetCoupling.Cli.Analysis;

public static class GitVolatility
{
    public static IReadOnlyDictionary<string, int> GetChangeCounts(string repositoryPath, int months)
    {
        try
        {
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

            Dictionary<string, int> counts = new(StringComparer.Ordinal);
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string fullPath = Path.GetFullPath(Path.Combine(repositoryPath, line));
                counts[fullPath] = counts.GetValueOrDefault(fullPath) + 1;
            }

            return counts;
        }
        catch
        {
            /* git not available or repository unavailable; skip volatility analysis */
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }
}
