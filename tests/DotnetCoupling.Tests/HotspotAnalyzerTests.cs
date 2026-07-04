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
