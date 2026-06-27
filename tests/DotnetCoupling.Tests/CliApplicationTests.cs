using System.Diagnostics;
using DotnetCoupling.Cli;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_SummaryNoGit_ReturnsSummaryOutput()
    {
        CommandResult result = await RunCliAsync("--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Grade: C", result.Output);
        Assert.Contains("Git: disabled (--no-git)", result.Output);
        Assert.DoesNotContain("{", result.Output);
    }

    [Fact]
    public async Task RunAsync_JsonTakesPrecedenceOverSummaryAndCheck()
    {
        CommandResult result = await RunCliAsync("--json", "--summary", "--check", "--min-grade", "B", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"schemaVersion\": \"0.1\"", result.Output);
        Assert.StartsWith("{", result.Output.TrimStart(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CheckMinGrade_ReturnsFailureWhenGradeIsTooLow()
    {
        CommandResult result = await RunCliAsync("--check", "--min-grade", "B", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Grade: C", result.Output);
    }

    [Fact]
    public async Task RunAsync_FailOnMedium_ReturnsFailureWhenMediumIssueExists()
    {
        CommandResult result = await RunCliAsync("--check", "--fail-on", "Medium", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidFailOn_ReturnsCliArgumentError()
    {
        CommandResult result = await RunCliAsync("--check", "--fail-on", "Severe", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid severity", result.Error);
    }

    [Fact]
    public async Task RunAsync_MissingPath_ReturnsPathError()
    {
        CommandResult result = await RunCliAsync("--summary", Path.Combine(TestPaths.RepositoryRoot, "tests", "fixtures", "missing"));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Target path does not exist", result.Error);
    }

    [Fact]
    public async Task RunAsync_InvalidConfig_ReturnsCliArgumentError()
    {
        string directory = CreateDirectory();
        string sourcePath = Path.Combine(directory, "Sample.cs");
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(sourcePath, "namespace Sample; public sealed class Type { }");
        File.WriteAllText(configPath, """{ "unexpected": true }""");

        CommandResult result = await RunCliAsync("--summary", "--config", configPath, "--no-git", directory);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown configuration property", result.Error);
    }

    [Fact]
    public async Task RunAsync_BaselineCheckFailsWhenNewIssueMeetsFailOnThreshold()
    {
        string repository = CreateGitRepository();
        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
            }
            """);
        Commit(repository, "baseline");
        RunGit(repository, "branch", "baseline");

        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    _ = new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(repository, "src", "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
            }
            """);
        Commit(repository, "add new issue");

        CommandResult result = await RunCliAsync("--check", "--baseline", "baseline", "--fail-on", "Medium", "--no-git", Path.Combine(repository, "src"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Baseline: baseline", result.Output);
        Assert.Contains("New Issues: 0 Critical, 0 High, 1 Medium", result.Output);
    }

    [Fact]
    public async Task RunAsync_BaselineCheckPassesWhenOnlyExistingIssuesRemain()
    {
        string repository = CreateGitRepository();
        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    _ = new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(repository, "src", "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
            }
            """);
        Commit(repository, "baseline");
        RunGit(repository, "branch", "baseline");
        File.AppendAllText(Path.Combine(repository, "README.md"), "change");
        Commit(repository, "non code change");

        CommandResult result = await RunCliAsync("--check", "--baseline", "baseline", "--no-git", Path.Combine(repository, "src"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Unchanged Issues: 1", result.Output);
    }

    private static async Task<CommandResult> RunCliAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exitCode = await CliApplication.RunAsync(args);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateGitRepository()
    {
        string repositoryPath = CreateDirectory();
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
