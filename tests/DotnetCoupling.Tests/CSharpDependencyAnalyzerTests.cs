using DotnetCoupling.Cli.Analysis;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CSharpDependencyAnalyzerTests
{
    [Fact]
    public void Analyze_TwoTypesWithConstructorDependency_ReportsComponentAndCoupling()
    {
        string directory = CreateFixture("""
            namespace Sample.App;

            public sealed class Handler
            {
                public Handler(IRepository repository)
                {
                }
            }

            public interface IRepository
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(directory, useGit: false, gitMonths: 6);

        Assert.Equal(2, report.Components.Count);
        Assert.Contains(report.Couplings, coupling =>
            coupling.Source == "Sample.App.Handler"
            && coupling.Target == "Sample.App.IRepository"
            && coupling.Strength == IntegrationStrength.Contract);
    }

    [Fact]
    public void Render_JsonOutput_IncludesSchemaVersionAndSyntaxOnlyMode()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 1, 1, 0, 0, false, 6),
            new GradeResult("B", "Bootstrap", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            ["Semantic symbol resolution is not enabled."]);

        string json = ReportRenderer.Render(report, ReportFormat.Json);

        Assert.Contains("\"$schema\"", json);
        Assert.Contains("\"schemaVersion\": \"0.1\"", json);
        Assert.Contains("\"mode\": \"syntax-only\"", json);
    }

    private static string CreateFixture(string source)
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Sample.cs"), source);
        return directory;
    }
}
