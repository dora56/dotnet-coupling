using System.Text.Json;
using System.Diagnostics;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CiSummaryScriptTests
{
    [Fact]
    public async Task GenerateCiSummary_NoCouplingArtifacts_ReportsCouplingFeedbackUnavailable()
    {
        string directory = CreateDirectory();
        string mutationDirectory = Path.Combine(directory, "mutation");
        string couplingDirectory = Path.Combine(directory, "coupling");

        CommandResult result = await RunSummaryScriptAsync(mutationDirectory, couplingDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Coupling feedback | not available for this run |", result.Output);
        Assert.Contains("- Status: not available for this run", result.Output);
        Assert.DoesNotContain("| Coverage |", result.Output);
    }

    [Fact]
    public async Task GenerateCiSummary_WithCouplingArtifacts_IncludesHotspotExcerpt()
    {
        string directory = CreateDirectory();
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

        CommandResult result = await RunSummaryScriptAsync(mutationDirectory, couplingDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Coupling feedback | 1 SARIF / 1 hotspots artifact(s) |", result.Output);
        Assert.Contains("Sample.Api.Handler", result.Output);
    }

    [Fact]
    public async Task GenerateBranchCoverageMetric_ValidCobertura_EmitsOctocovMetric()
    {
        string directory = CreateDirectory();
        string coveragePath = Path.Combine(directory, "coverage.xml");
        string outputPath = Path.Combine(directory, "branch-coverage.json");
        File.WriteAllText(coveragePath, """<coverage branches-covered="33" branches-valid="40" />""");

        CommandResult result = await RunBranchMetricScriptAsync(coveragePath, outputPath);

        Assert.Equal(0, result.ExitCode);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal("branch_coverage", document.RootElement.GetProperty("key").GetString());
        Assert.Equal(82.5, document.RootElement.GetProperty("metrics")[0].GetProperty("value").GetDouble());
        Assert.Equal("current.branch_coverage >= 80", document.RootElement.GetProperty("acceptables")[0].GetString());
        Assert.Equal("diff.branch_coverage >= -0.1", document.RootElement.GetProperty("acceptables")[1].GetString());
    }

    [Theory]
    [InlineData("<coverage branches-covered=\"32\" branches-valid=\"40\" />", 0)]
    [InlineData("<coverage branches-covered=\"31\" branches-valid=\"40\" />", 0)]
    [InlineData("<coverage>", 1)]
    [InlineData("<coverage />", 1)]
    [InlineData("<coverage branches-covered=\"41\" branches-valid=\"40\" />", 1)]
    public async Task GenerateBranchCoverageMetric_BoundaryAndMalformedInput_ReturnsExpectedExitCode(
        string coverage,
        int expectedExitCode)
    {
        string directory = CreateDirectory();
        string coveragePath = Path.Combine(directory, "coverage.xml");
        string outputPath = Path.Combine(directory, "branch-coverage.json");
        File.WriteAllText(coveragePath, coverage);

        CommandResult result = await RunBranchMetricScriptAsync(coveragePath, outputPath);

        Assert.Equal(expectedExitCode, result.ExitCode);
    }

    [Fact]
    public async Task GenerateBranchCoverageMetric_MissingInput_ReturnsError()
    {
        string directory = CreateDirectory();

        CommandResult result = await RunBranchMetricScriptAsync(
            Path.Combine(directory, "missing.xml"),
            Path.Combine(directory, "branch-coverage.json"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Could not read branch coverage", result.Error);
    }

    [Fact]
    public async Task GenerateCiSummary_SemanticReport_IncludesGradeAndIssueCounts()
    {
        string directory = CreateDirectory();
        string semanticDirectory = Path.Combine(directory, "semantic");
        Directory.CreateDirectory(semanticDirectory);
        File.WriteAllText(
            Path.Combine(semanticDirectory, "semantic-self-report.json"),
            """
            {
              "grade": { "letter": "C" },
              "issues": [
                { "severity": "Critical" },
                { "severity": "High" },
                { "severity": "Medium" },
                { "severity": "Medium" }
              ]
            }
            """);

        CommandResult result = await RunSummaryScriptAsync(
            Path.Combine(directory, "mutation"),
            Path.Combine(directory, "coupling"),
            semanticDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Semantic self | Grade C / 1 Critical / 1 High / 2 Medium |", result.Output);
    }

    private static async Task<CommandResult> RunSummaryScriptAsync(
        string mutationDirectory,
        string couplingDirectory,
        string? semanticDirectory = null)
    {
        ProcessStartInfo startInfo = new("bash")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(Path.Combine(TestPaths.RepositoryRoot, "scripts", "generate-ci-summary.sh"));
        startInfo.ArgumentList.Add("--mutation-dir");
        startInfo.ArgumentList.Add(mutationDirectory);
        startInfo.ArgumentList.Add("--coupling-dir");
        startInfo.ArgumentList.Add(couplingDirectory);
        if (semanticDirectory is not null)
        {
            startInfo.ArgumentList.Add("--semantic-dir");
            startInfo.ArgumentList.Add(semanticDirectory);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start summary script.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CommandResult(process.ExitCode, output, error);
    }

    private static async Task<CommandResult> RunBranchMetricScriptAsync(string coveragePath, string outputPath)
    {
        ProcessStartInfo startInfo = new("python3")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(Path.Combine(TestPaths.RepositoryRoot, "scripts", "generate-branch-coverage-metric.py"));
        startInfo.ArgumentList.Add("--coverage");
        startInfo.ArgumentList.Add(coveragePath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start branch metric script.");
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
