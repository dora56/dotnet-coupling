using DotnetCoupling.Core;
using DotnetCoupling.Roslyn;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class HotspotAnalyzerTests
{
    [Fact]
    public void Calculate_GlobalComplexityReport_RanksIssueParticipants()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        IReadOnlyList<Hotspot> hotspots = HotspotAnalyzer.Calculate(report, count: 10);

        Assert.NotEmpty(hotspots);
        Assert.Equal(1, hotspots[0].Rank);
        Assert.Contains(hotspots, hotspot => hotspot.Component == "Fixture.Global.Api.Handler");
        Assert.All(hotspots, hotspot => Assert.InRange(hotspot.Score, 0.0, 1.0));
        Assert.All(hotspots, hotspot => Assert.Contains("active issue", string.Join(", ", hotspot.Reasons), StringComparison.Ordinal));
    }

    [Fact]
    public void Calculate_CountLimit_ReturnsRequestedNumber()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        IReadOnlyList<Hotspot> hotspots = HotspotAnalyzer.Calculate(report, count: 1);

        Assert.Single(hotspots);
        Assert.Equal(1, hotspots[0].Rank);
    }

    [Fact]
    public void Calculate_DefaultCount_UsesPublicDefault()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        IReadOnlyList<Hotspot> defaultHotspots = HotspotAnalyzer.Calculate(report);
        IReadOnlyList<Hotspot> explicitHotspots = HotspotAnalyzer.Calculate(report, HotspotAnalyzer.DefaultCount);

        Assert.Equal(explicitHotspots.Select(ToComparable), defaultHotspots.Select(ToComparable));
    }

    [Fact]
    public void Calculate_ReportWithRoleContext_AddsRoleReasonWithoutChangingScore()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        Hotspot baseline = Assert.Single(
            HotspotAnalyzer.Calculate(report, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");
        AnalysisReport reportWithRole = report with
        {
            ComponentRoles =
            [
                new ComponentRoleContext(
                    "Fixture.Global.Api.Handler",
                    Path.Combine(fixture, "Api", "Handler.cs"),
                    SubdomainName: null,
                    StrategicRole: null,
                    AreaName: "Application",
                    TechnicalRole: TechnicalRole.ApplicationService),
            ],
        };

        Hotspot hotspotWithRole = Assert.Single(
            HotspotAnalyzer.Calculate(reportWithRole, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");

        Assert.Equal(baseline.Score, hotspotWithRole.Score);
        Assert.Contains("technical role: application_service", hotspotWithRole.Reasons);
    }

    [Fact]
    public void Calculate_ReportWithHighComplexity_AddsComplexityReasonAndPriorityBonus()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        Hotspot baseline = Assert.Single(
            HotspotAnalyzer.Calculate(report, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");
        MemberComplexity member = new(
            "Fixture.Global.Api.Handler",
            "Handle",
            new SourceLocation(Path.Combine(fixture, "Api", "Handler.cs"), 5),
            CyclomaticComplexity: 12,
            CognitiveComplexity: 16);
        AnalysisReport reportWithComplexity = report with
        {
            ComponentComplexities =
            [
                new ComponentComplexity(
                    "Fixture.Global.Api.Handler",
                    Path.Combine(fixture, "Api", "Handler.cs"),
                    MemberCount: 1,
                    MaxCyclomaticComplexity: 12,
                    MaxCognitiveComplexity: 16,
                    TotalCyclomaticComplexity: 12,
                    TotalCognitiveComplexity: 16,
                    MostComplexMember: member),
            ],
        };

        Hotspot hotspotWithComplexity = Assert.Single(
            HotspotAnalyzer.Calculate(reportWithComplexity, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");

        Assert.True(hotspotWithComplexity.Score > baseline.Score);
        Assert.Contains("high cyclomatic complexity: 12", hotspotWithComplexity.Reasons);
        Assert.Contains("high cognitive complexity: 16", hotspotWithComplexity.Reasons);
        Assert.NotNull(hotspotWithComplexity.Complexity);
        Assert.Equal("Handle", hotspotWithComplexity.Complexity.MostComplexMember);
    }

    [Fact]
    public void Calculate_ReportWithLowComplexity_KeepsPriorityScore()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        Hotspot baseline = Assert.Single(
            HotspotAnalyzer.Calculate(report, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");
        AnalysisReport reportWithComplexity = report with
        {
            ComponentComplexities =
            [
                new ComponentComplexity(
                    "Fixture.Global.Api.Handler",
                    Path.Combine(fixture, "Api", "Handler.cs"),
                    MemberCount: 1,
                    MaxCyclomaticComplexity: 1,
                    MaxCognitiveComplexity: 0,
                    TotalCyclomaticComplexity: 1,
                    TotalCognitiveComplexity: 0,
                    MostComplexMember: null),
            ],
        };

        Hotspot hotspotWithComplexity = Assert.Single(
            HotspotAnalyzer.Calculate(reportWithComplexity, count: 10),
            hotspot => hotspot.Component == "Fixture.Global.Api.Handler");

        Assert.Equal(baseline.Score, hotspotWithComplexity.Score);
        Assert.DoesNotContain(hotspotWithComplexity.Reasons, reason => reason.Contains("complexity", StringComparison.Ordinal));
    }

    [Fact]
    public void Calculate_FilePathHotspot_UsesComplexityFromComponentsInThatFile()
    {
        string filePath = "/tmp/sample/Handler.cs";
        CouplingIssue issue = new(
            IssueType.HiddenCoupling,
            Severity.Medium,
            filePath,
            "/tmp/sample/Repository.cs",
            0.50,
            "Problem",
            "Recommendation",
            new SourceLocation(filePath, 1));
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 0, 0, true, true, 6),
            new GradeResult("C", "Needs attention", "issue-density", "Test"),
            0.50,
            [],
            [],
            [],
            [issue],
            [],
            ComponentComplexities:
            [
                new ComponentComplexity(
                    "Sample.Handler",
                    filePath,
                    MemberCount: 1,
                    MaxCyclomaticComplexity: 11,
                    MaxCognitiveComplexity: 3,
                    TotalCyclomaticComplexity: 11,
                    TotalCognitiveComplexity: 3,
                    MostComplexMember: new MemberComplexity("Sample.Handler", "Handle", new SourceLocation(filePath, 5), 11, 3)),
            ]);

        Hotspot hotspot = Assert.Single(
            HotspotAnalyzer.Calculate(report, count: 10),
            item => item.Component == filePath);

        Assert.NotNull(hotspot.Complexity);
        Assert.Equal(11, hotspot.Complexity.MaxCyclomaticComplexity);
        Assert.Contains("high cyclomatic complexity: 11", hotspot.Reasons);
    }

    [Fact]
    public void Calculate_NonPositiveCount_Throws()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        Assert.Throws<ArgumentOutOfRangeException>(() => HotspotAnalyzer.Calculate(report, count: 0));
    }

    private static object ToComparable(Hotspot hotspot)
    {
        return new
        {
            hotspot.Rank,
            hotspot.Component,
            hotspot.Score,
            hotspot.IssueCount,
            hotspot.FanIn,
            hotspot.FanOut,
            hotspot.Volatility,
            hotspot.CrossesBoundary,
            hotspot.ParticipatesInCycle,
            Reasons = string.Join("|", hotspot.Reasons),
        };
    }
}
