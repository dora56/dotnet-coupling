using System.Diagnostics;
using DotnetCoupling.Cli.Analysis;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class GitVolatilityTests
{
    [Fact]
    public void GetChangeCounts_GitRepository_CountsCommittedCSharpFileChanges()
    {
        string repositoryPath = CreateGitRepository();
        string first = Path.GetFullPath(Path.Combine(repositoryPath, "src", "First.cs"));
        string second = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Second.cs"));

        WriteFile(first, "public sealed class First { }");
        WriteFile(second, "public sealed class Second { }");
        Commit(repositoryPath, "initial");
        WriteFile(first, "public sealed class First { public int Value => 1; }");
        Commit(repositoryPath, "change first");

        IReadOnlyDictionary<string, int> counts = GitVolatility.GetChangeCounts(repositoryPath, months: 120);

        Assert.Equal(2, counts[first]);
        Assert.Equal(1, counts[second]);
    }

    [Fact]
    public void GetChangeCounts_SubdirectoryTarget_UsesRepositoryRootRelativePaths()
    {
        string repositoryPath = CreateGitRepository();
        string sourcePath = Path.Combine(repositoryPath, "src");
        string first = Path.GetFullPath(Path.Combine(sourcePath, "First.cs"));

        WriteFile(first, "public sealed class First { }");
        Commit(repositoryPath, "initial");

        IReadOnlyDictionary<string, int> counts = GitVolatility.GetChangeCounts(sourcePath, months: 120);

        Assert.Equal(1, counts[first]);
    }

    [Fact]
    public void GetTemporalCouplings_GitRepository_ReturnsRepeatedCSharpCoChanges()
    {
        string repositoryPath = CreateGitRepository();
        string first = Path.GetFullPath(Path.Combine(repositoryPath, "src", "First.cs"));
        string second = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Second.cs"));
        HashSet<string> analyzedFiles = [first, second];

        for (int i = 0; i < 3; i++)
        {
            WriteFile(first, $"public sealed class First {{ public int Value => {i}; }}");
            WriteFile(second, $"public sealed class Second {{ public int Value => {i}; }}");
            Commit(repositoryPath, $"co-change {i}");
        }

        IReadOnlyList<TemporalCoupling> couplings = GitVolatility.GetTemporalCouplings(
            repositoryPath,
            months: 120,
            analyzedFiles,
            minTemporalCoupling: 3);

        TemporalCoupling coupling = Assert.Single(couplings);
        Assert.Equal(first, coupling.FileA);
        Assert.Equal(second, coupling.FileB);
        Assert.Equal(3, coupling.CoChangeCount);
    }

    [Fact]
    public void GetTemporalCouplings_SubdirectoryTarget_UsesRepositoryRootRelativePaths()
    {
        string repositoryPath = CreateGitRepository();
        string sourcePath = Path.Combine(repositoryPath, "src");
        string first = Path.GetFullPath(Path.Combine(sourcePath, "First.cs"));
        string second = Path.GetFullPath(Path.Combine(sourcePath, "Second.cs"));
        HashSet<string> analyzedFiles = [first, second];

        WriteFile(first, "public sealed class First { }");
        WriteFile(second, "public sealed class Second { }");
        Commit(repositoryPath, "initial");

        IReadOnlyList<TemporalCoupling> couplings = GitVolatility.GetTemporalCouplings(
            sourcePath,
            months: 120,
            analyzedFiles,
            minTemporalCoupling: 1);

        TemporalCoupling coupling = Assert.Single(couplings);
        Assert.Equal(first, coupling.FileA);
        Assert.Equal(second, coupling.FileB);
        Assert.Equal(1, coupling.CoChangeCount);
    }

    [Fact]
    public void AnalyzeChangeCountsFromLog_RepeatedFiles_CountsChanges()
    {
        string repositoryPath = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string first = Path.GetFullPath(Path.Combine(repositoryPath, "src", "First.cs"));
        string second = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Second.cs"));

        IReadOnlyDictionary<string, int> counts = GitVolatility.AnalyzeChangeCountsFromLog(
            repositoryPath,
            """
            src/First.cs
            src/Second.cs
            src/First.cs
            """);

        Assert.Equal(2, counts[first]);
        Assert.Equal(1, counts[second]);
    }

    [Fact]
    public void AnalyzeTemporalCouplingsFromLog_RepeatedCoChanges_ReturnsThresholdPairs()
    {
        string repositoryPath = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string first = Path.GetFullPath(Path.Combine(repositoryPath, "src", "First.cs"));
        string second = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Second.cs"));
        string third = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Third.cs"));
        HashSet<string> analyzedFiles = [first, second, third];
        string gitLog = """
            COMMIT:1
            src/First.cs
            src/Second.cs
            COMMIT:2
            src/First.cs
            src/Second.cs
            src/Third.cs
            COMMIT:3
            src/First.cs
            src/Second.cs
            """;

        IReadOnlyList<TemporalCoupling> couplings = GitVolatility.AnalyzeTemporalCouplingsFromLog(
            repositoryPath,
            gitLog,
            analyzedFiles,
            minTemporalCoupling: 3,
            maxTemporalFilesPerCommit: 50);

        TemporalCoupling coupling = Assert.Single(couplings);
        Assert.Equal(first, coupling.FileA);
        Assert.Equal(second, coupling.FileB);
        Assert.Equal(3, coupling.CoChangeCount);
    }

    [Fact]
    public void AnalyzeTemporalCouplingsFromLog_ThresholdHugeCommitAndUnanalyzedFiles_AreIgnored()
    {
        string repositoryPath = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string first = Path.GetFullPath(Path.Combine(repositoryPath, "src", "First.cs"));
        string second = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Second.cs"));
        string third = Path.GetFullPath(Path.Combine(repositoryPath, "src", "Third.cs"));
        HashSet<string> analyzedFiles = [first, second, third];
        string gitLog = """
            COMMIT:below-threshold
            src/First.cs
            src/Second.cs
            COMMIT:huge
            src/First.cs
            src/Second.cs
            src/Third.cs
            COMMIT:unanalyzed
            src/First.cs
            src/Unanalyzed.cs
            """;

        IReadOnlyList<TemporalCoupling> couplings = GitVolatility.AnalyzeTemporalCouplingsFromLog(
            repositoryPath,
            gitLog,
            analyzedFiles,
            minTemporalCoupling: 2,
            maxTemporalFilesPerCommit: 2);

        Assert.Empty(couplings);
    }

    private static string CreateGitRepository()
    {
        string repositoryPath = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryPath);
        RunGit(repositoryPath, "init");
        RunGit(repositoryPath, "config", "user.email", "tests@example.invalid");
        RunGit(repositoryPath, "config", "user.name", "Dotnet Coupling Tests");
        return repositoryPath;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void Commit(string repositoryPath, string message)
    {
        RunGit(repositoryPath, "add", ".");
        RunGit(repositoryPath, "commit", "-m", message);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.\nSTDOUT:\n{standardOutput}\nSTDERR:\n{standardError}");
    }
}
