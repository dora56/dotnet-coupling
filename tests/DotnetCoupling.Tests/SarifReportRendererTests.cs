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
        (string fixture, AnalysisReport report) = AnalyzeGlobalComplexityFixture();
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
        (string fixture, AnalysisReport report) = AnalyzeGlobalComplexityFixture();

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

    [Fact]
    public void Render_ReportWithUnlocatableIssue_OmitsResultAndRecordsOmittedCount()
    {
        CouplingIssue locatableIssue = CreateIssue(
            IssueType.GlobalComplexity,
            Severity.Medium,
            "Sample.Api.Handler",
            "Sample.Infrastructure.Repository",
            new SourceLocation("/tmp/sample/Api.cs", 1));
        CouplingIssue unlocatableIssue = CreateIssue(
            IssueType.HighAfferentCoupling,
            Severity.High,
            "",
            "Sample.Shared.Model",
            location: null);
        AnalysisReport report = CreateReport(locatableIssue, unlocatableIssue);

        using JsonDocument document = JsonDocument.Parse(SarifReportRenderer.Render(report, "/tmp/sample"));

        JsonElement run = document.RootElement.GetProperty("runs")[0];
        Assert.Single(run.GetProperty("results").EnumerateArray());
        Assert.Equal(
            1,
            run.GetProperty("properties").GetProperty("dotnetCouplingOmittedIssueCount").GetInt32());
    }

    private static (string Fixture, AnalysisReport Report) AnalyzeGlobalComplexityFixture()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        return (fixture, CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6));
    }

    private static AnalysisReport CreateReport(params CouplingIssue[] issues)
    {
        return new AnalysisReport(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 3, 1, 0, false, false, 6),
            new GradeResult("D", "High risk", "issue-density", "Test"),
            0.50,
            [],
            [],
            [],
            issues,
            []);
    }

    private static CouplingIssue CreateIssue(
        IssueType type,
        Severity severity,
        string source,
        string target,
        SourceLocation? location)
    {
        return new CouplingIssue(
            type,
            severity,
            source,
            target,
            0.50,
            "Problem",
            "Recommendation",
            location);
    }
}
