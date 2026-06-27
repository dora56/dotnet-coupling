using DotnetCoupling.Cli.Analysis;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class AnalyzerFixtureTests
{
    [Fact]
    public void Analyze_GlobalComplexityFixture_ReportsGlobalComplexity()
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(TestPaths.Fixture("global-complexity"), useGit: false, gitMonths: 6);

        Assert.Contains(report.Issues, issue => issue.Type == IssueType.GlobalComplexity);
        Assert.Equal(1, report.Summary.InternalCouplings);
        Assert.Equal(0, report.Summary.ExternalCouplings);
    }

    [Fact]
    public void Analyze_CircularDependencyFixture_ReportsCircularDependency()
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(TestPaths.Fixture("circular-dependency"), useGit: false, gitMonths: 6);

        Assert.Contains(report.Issues, issue => issue.Type == IssueType.CircularDependency);
    }

    [Fact]
    public void Analyze_GeneratedCodeFixture_ExcludesGeneratedFiles()
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(TestPaths.Fixture("generated-code"), useGit: false, gitMonths: 6);

        Component component = Assert.Single(report.Components);
        Assert.Equal("RealType", component.Name);
    }

    [Fact]
    public void Analyze_ScatteredExternalFixture_ReportsExternalCouplingsWithoutGradeDenominator()
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(TestPaths.Fixture("scattered-external"), useGit: false, gitMonths: 6);

        Assert.Equal(0, report.Summary.InternalCouplings);
        Assert.Equal(5, report.Summary.ExternalCouplings);
        Assert.Contains(report.Issues, issue => issue.Type == IssueType.ScatteredExternalCoupling);
        Assert.Equal("C", report.Grade.Letter);
    }

    [Fact]
    public void Analyze_HiddenCouplingFixture_HasNoExplicitCouplings()
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(TestPaths.Fixture("hidden-coupling"), useGit: false, gitMonths: 6);

        Assert.Empty(report.Couplings);
        Assert.Empty(report.Issues);
    }
}
