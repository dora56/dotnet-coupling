using DotnetCoupling.Core;
using DotnetCoupling.Roslyn;
using DotnetCoupling.Sarif;
using Microsoft.CodeAnalysis.Sarif;
using System.Text.Json;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class SarifReportRendererTests
{
    [Fact]
    public void Render_GlobalComplexityReport_ProducesRoundTrippableSarif()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        string sarif = SarifReportRenderer.Render(report, fixture);
        string sarifPath = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"), "report.sarif");
        Directory.CreateDirectory(Path.GetDirectoryName(sarifPath)!);
        File.WriteAllText(sarifPath, sarif);

        SarifLog roundTripped = SarifLog.Load(sarifPath);

        Assert.Equal(SarifVersion.Current, roundTripped.Version);
        Run run = Assert.Single(roundTripped.Runs);
        Assert.Equal("dotnet-coupling", run.Tool.Driver.Name);
        Assert.Contains(run.Tool.Driver.Rules, rule => rule.Id == "GlobalComplexity");
        Result result = Assert.Single(run.Results);
        Assert.Equal("GlobalComplexity", result.RuleId);
        Assert.Equal(FailureLevel.Warning, result.Level);
        Assert.Equal("Fixture.Global.Api.Handler", result.PartialFingerprints["dotnetCouplingSource"]);
    }

    [Fact]
    public void Render_GlobalComplexityReport_UsesRepositoryRelativeLocation()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(SarifReportRenderer.Render(report, fixture));

        JsonElement root = document.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        string? uri = result.GetProperty("locations")[0]
            .GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri")
            .GetString();
        Assert.Equal("Api/Handler.cs", uri);
        Assert.True(result.GetProperty("partialFingerprints").TryGetProperty("dotnetCouplingIssueKey", out _));
    }
}
