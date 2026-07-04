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
    public void Calculate_NonPositiveCount_Throws()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        Assert.Throws<ArgumentOutOfRangeException>(() => HotspotAnalyzer.Calculate(report, count: 0));
    }
}
