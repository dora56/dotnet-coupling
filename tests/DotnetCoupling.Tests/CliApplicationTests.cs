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
}
