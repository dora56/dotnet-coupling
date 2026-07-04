using System.Diagnostics;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CiSummaryScriptTests
{
    [Fact]
    public async Task GenerateCiSummary_NoCouplingArtifacts_ReportsCouplingFeedbackUnavailable()
    {
        string directory = CreateDirectory();
        string coverageDirectory = CreateCoverageDirectory(directory);
        string mutationDirectory = Path.Combine(directory, "mutation");
        string couplingDirectory = Path.Combine(directory, "coupling");

        CommandResult result = await RunSummaryScriptAsync(coverageDirectory, mutationDirectory, couplingDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Coupling feedback | not available for this run |", result.Output);
        Assert.Contains("- Status: not available for this run", result.Output);
    }

    [Fact]
    public async Task GenerateCiSummary_WithCouplingArtifacts_IncludesHotspotExcerpt()
    {
        string directory = CreateDirectory();
        string coverageDirectory = CreateCoverageDirectory(directory);
        string mutationDirectory = Path.Combine(directory, "mutation");
        string couplingDirectory = Path.Combine(directory, "coupling");
        Directory.CreateDirectory(couplingDirectory);
        File.WriteAllText(Path.Combine(couplingDirectory, "dotnet-coupling.sarif"), "{}");
        File.WriteAllText(
            Path.Combine(couplingDirectory, "dotnet-coupling-hotspots.txt"),
            """
            Hotspots
            1. Sample.Api.Handler score=0.80
            """);

        CommandResult result = await RunSummaryScriptAsync(coverageDirectory, mutationDirectory, couplingDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Coupling feedback | 1 SARIF / 1 hotspots artifact(s) |", result.Output);
        Assert.Contains("Sample.Api.Handler", result.Output);
    }

    private static string CreateCoverageDirectory(string directory)
    {
        string coverageDirectory = Path.Combine(directory, "coverage");
        Directory.CreateDirectory(coverageDirectory);
        File.WriteAllText(
            Path.Combine(coverageDirectory, "coverage-1.cobertura.xml"),
            """<coverage lines-covered="8" lines-valid="10" branches-covered="2" branches-valid="4" />""");
        return coverageDirectory;
    }

    private static async Task<CommandResult> RunSummaryScriptAsync(
        string coverageDirectory,
        string mutationDirectory,
        string couplingDirectory)
    {
        ProcessStartInfo startInfo = new("bash")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(Path.Combine(TestPaths.RepositoryRoot, "scripts", "generate-ci-summary.sh"));
        startInfo.ArgumentList.Add("--coverage-dir");
        startInfo.ArgumentList.Add(coverageDirectory);
        startInfo.ArgumentList.Add("--mutation-dir");
        startInfo.ArgumentList.Add(mutationDirectory);
        startInfo.ArgumentList.Add("--coupling-dir");
        startInfo.ArgumentList.Add(couplingDirectory);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start summary script.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CommandResult(process.ExitCode, output, error);
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
