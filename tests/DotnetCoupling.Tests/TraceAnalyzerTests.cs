using DotnetCoupling.Core;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class TraceAnalyzerTests
{
    [Fact]
    public void Analyze_MemberSymbol_ReturnsDirectAndTransitiveCallers()
    {
        AnalysisReport report = CreateSemanticReport(
            components:
            [
                Component("Sample.Infrastructure.Repository", "Repository", "Sample.Infrastructure", "Infrastructure"),
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
                Component("Sample.Api.OrderEndpoint", "OrderEndpoint", "Sample.Api", "Api"),
            ],
            observations:
            [
                new DependencyObservation(
                    "Sample.Application.OrderService",
                    "Sample.Infrastructure.Repository",
                    DependencyKind.MethodCall,
                    UsageContext.MethodCall,
                    "/tmp/Application/OrderService.cs",
                    12,
                    "repository.Save()",
                    "Sample.Application.OrderService.Handle",
                    "Sample.Infrastructure.Repository.Save"),
            ],
            couplings:
            [
                Coupling("Sample.Application.OrderService", "Sample.Infrastructure.Repository", "Application", "Infrastructure"),
                Coupling("Sample.Api.OrderEndpoint", "Sample.Application.OrderService", "Api", "Application"),
            ]);

        TraceAnalysis trace = TraceAnalyzer.Analyze(report, "Repository.Save", maxDepth: 3);

        Assert.Equal("Sample.Infrastructure.Repository.Save", trace.Symbol);
        Assert.Equal(TracedSymbolKind.Member, trace.Kind);
        DependencyPath direct = Assert.Single(trace.Callers, caller => caller.Component == "Sample.Application.OrderService");
        Assert.Equal(1, direct.Depth);
        Assert.Equal("Sample.Application.OrderService.Handle", direct.SourceSymbol);
        Assert.Equal(new SourceLocation("/tmp/Application/OrderService.cs", 12), direct.Location);
        DependencyPath transitive = Assert.Single(trace.Callers, caller => caller.Component == "Sample.Api.OrderEndpoint");
        Assert.Equal(2, transitive.Depth);
        Assert.Equal(
            ["Sample.Infrastructure.Repository.Save", "Sample.Application.OrderService", "Sample.Api.OrderEndpoint"],
            transitive.Path);
    }

    [Fact]
    public void Analyze_TypeSymbol_UsesResolvedCouplings()
    {
        AnalysisReport report = CreateSemanticReport(
            components:
            [
                Component("Sample.Infrastructure.Repository", "Repository", "Sample.Infrastructure", "Infrastructure"),
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
            ],
            observations: [],
            couplings:
            [
                Coupling("Sample.Application.OrderService", "Sample.Infrastructure.Repository", "Application", "Infrastructure"),
            ]);

        TraceAnalysis trace = TraceAnalyzer.Analyze(report, "Repository", maxDepth: 3);

        Assert.Equal("Sample.Infrastructure.Repository", trace.Symbol);
        Assert.Equal(TracedSymbolKind.Component, trace.Kind);
        DependencyPath caller = Assert.Single(trace.Callers);
        Assert.Equal("Sample.Application.OrderService", caller.Component);
        Assert.Equal(1, caller.Depth);
    }

    [Fact]
    public void Analyze_AmbiguousMemberName_ReportsSortedCandidates()
    {
        AnalysisReport report = CreateSemanticReport(
            components:
            [
                Component("Sales.Repository", "Repository", "Sales", "Sales"),
                Component("Support.Repository", "Repository", "Support", "Support"),
                Component("Sample.Caller", "Caller", "Sample", "Sample"),
            ],
            observations:
            [
                Observation("Sample.Caller", "Sales.Repository", "Sales.Repository.Save"),
                Observation("Sample.Caller", "Support.Repository", "Support.Repository.Save"),
            ],
            couplings: []);

        InvestigationQueryException exception = Assert.Throws<InvestigationQueryException>(() =>
            TraceAnalyzer.Analyze(report, "Save", maxDepth: 3));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(exception.Message.IndexOf("Sales.Repository.Save", StringComparison.Ordinal) < exception.Message.IndexOf("Support.Repository.Save", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_SyntaxReport_RequiresSemanticMode()
    {
        AnalysisReport report = CreateSemanticReport([], [], []) with
        {
            Summary = new AnalysisSummary("/tmp/sample", "syntax-only", 0, 0, 0, 0, false, false, 6),
        };

        InvestigationQueryException exception = Assert.Throws<InvestigationQueryException>(() =>
            TraceAnalyzer.Analyze(report, "Repository", maxDepth: 3));

        Assert.Contains("semantic mode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AnalysisReport CreateSemanticReport(
        IReadOnlyList<Component> components,
        IReadOnlyList<DependencyObservation> observations,
        IReadOnlyList<CouplingMetrics> couplings)
    {
        return new AnalysisReport(
            new AnalysisSummary("/tmp/sample", "semantic-preview", components.Count, components.Count, couplings.Count, 0, false, false, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.75,
            components,
            observations,
            couplings,
            [],
            []);
    }

    private static Component Component(string id, string name, string namespaceName, string projectName)
    {
        return new Component(id, name, namespaceName, projectName, $"/tmp/{projectName}/{name}.cs", ComponentKind.Class, Visibility.Public);
    }

    private static CouplingMetrics Coupling(
        string source,
        string target,
        string sourceProject,
        string targetProject)
    {
        return new CouplingMetrics(
            source,
            target,
            IntegrationStrength.Functional,
            Distance.DifferentProject,
            Volatility.Medium,
            sourceProject,
            targetProject,
            Visibility.Public,
            new SourceLocation($"/tmp/{sourceProject}/Source.cs", 1));
    }

    private static DependencyObservation Observation(string source, string target, string targetSymbol)
    {
        return new DependencyObservation(
            source,
            target,
            DependencyKind.MethodCall,
            UsageContext.MethodCall,
            "/tmp/Caller.cs",
            1,
            "Save()",
            $"{source}.Call",
            targetSymbol);
    }
}
