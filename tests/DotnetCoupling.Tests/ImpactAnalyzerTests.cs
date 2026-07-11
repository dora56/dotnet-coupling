using DotnetCoupling.Core;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class ImpactAnalyzerTests
{
    [Fact]
    public void Analyze_ReverseDependencyGraph_ReturnsShortestCycleSafePaths()
    {
        AnalysisReport report = CreateReport(
            components:
            [
                Component("Sample.Domain.Order", "Order", "Sample.Domain", "Domain"),
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
                Component("Sample.Api.OrderEndpoint", "OrderEndpoint", "Sample.Api", "Api"),
            ],
            couplings:
            [
                Coupling("Sample.Application.OrderService", "Sample.Domain.Order", "Application", "Domain"),
                Coupling("Sample.Application.OrderService", "Sample.Domain.Order", "Application", "Domain", line: 2),
                Coupling("Sample.Api.OrderEndpoint", "Sample.Application.OrderService", "Api", "Application"),
                Coupling("Sample.Domain.Order", "Sample.Api.OrderEndpoint", "Domain", "Api"),
            ]);

        ImpactAnalysis impact = ImpactAnalyzer.Analyze(
            report,
            "Order",
            maxDepth: 0,
            PrioritizationOptions.Default);

        Assert.Equal("Sample.Domain.Order", impact.Component);
        Assert.Equal(2, impact.Dependents.Count);
        DependencyPath direct = Assert.Single(impact.Dependents, dependent => dependent.Component == "Sample.Application.OrderService");
        Assert.Equal(1, direct.Depth);
        Assert.Equal(["Sample.Domain.Order", "Sample.Application.OrderService"], direct.Path);
        DependencyPath transitive = Assert.Single(impact.Dependents, dependent => dependent.Component == "Sample.Api.OrderEndpoint");
        Assert.Equal(2, transitive.Depth);
        Assert.Equal(
            ["Sample.Domain.Order", "Sample.Application.OrderService", "Sample.Api.OrderEndpoint"],
            transitive.Path);
        Assert.Equal(["Api", "Application"], impact.Projects);
        Assert.Equal(["Sample.Api", "Sample.Application"], impact.Namespaces);
        Assert.InRange(impact.RiskScore, 0, 1);
    }

    [Fact]
    public void Analyze_MaxDepth_ReturnsOnlyComponentsWithinDepth()
    {
        AnalysisReport report = CreateReport(
            components:
            [
                Component("Sample.Domain.Order", "Order", "Sample.Domain", "Domain"),
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
                Component("Sample.Api.OrderEndpoint", "OrderEndpoint", "Sample.Api", "Api"),
            ],
            couplings:
            [
                Coupling("Sample.Application.OrderService", "Sample.Domain.Order", "Application", "Domain"),
                Coupling("Sample.Api.OrderEndpoint", "Sample.Application.OrderService", "Api", "Application"),
            ]);

        ImpactAnalysis impact = ImpactAnalyzer.Analyze(
            report,
            "Sample.Domain.Order",
            maxDepth: 1,
            PrioritizationOptions.Default);

        DependencyPath dependent = Assert.Single(impact.Dependents);
        Assert.Equal("Sample.Application.OrderService", dependent.Component);
        Assert.Equal(1, dependent.Depth);
    }

    [Fact]
    public void Analyze_TransitivePath_PreservesEarlierBoundaryCrossing()
    {
        AnalysisReport report = CreateReport(
            components:
            [
                Component("Sample.Domain.Order", "Order", "Sample.Domain", "Domain"),
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
                Component("Sample.Application.OrderFacade", "OrderFacade", "Sample.Application", "Application"),
            ],
            couplings:
            [
                Coupling("Sample.Application.OrderService", "Sample.Domain.Order", "Application", "Domain"),
                Coupling("Sample.Application.OrderFacade", "Sample.Application.OrderService", "Application", "Application", distance: Distance.SameNamespace),
            ]);

        ImpactAnalysis impact = ImpactAnalyzer.Analyze(report, "Order", maxDepth: 3, PrioritizationOptions.Default);

        DependencyPath transitive = Assert.Single(impact.Dependents, dependent => dependent.Component == "Sample.Application.OrderFacade");
        Assert.True(transitive.CrossesBoundary);
    }

    [Fact]
    public void Analyze_AmbiguousSimpleName_ReportsSortedCandidates()
    {
        AnalysisReport report = CreateReport(
            components:
            [
                Component("Sales.Handler", "Handler", "Sales", "Sales"),
                Component("Support.Handler", "Handler", "Support", "Support"),
            ],
            couplings: []);

        InvestigationQueryException exception = Assert.Throws<InvestigationQueryException>(() =>
            ImpactAnalyzer.Analyze(report, "Handler", maxDepth: 3, PrioritizationOptions.Default));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales.Handler", exception.Message);
        Assert.Contains("Support.Handler", exception.Message);
        Assert.True(exception.Message.IndexOf("Sales.Handler", StringComparison.Ordinal) < exception.Message.IndexOf("Support.Handler", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_HighComplexity_IncreasesRiskWithoutChangingGraph()
    {
        Component order = Component("Sample.Domain.Order", "Order", "Sample.Domain", "Domain");
        AnalysisReport report = CreateReport(
            components:
            [
                order,
                Component("Sample.Application.OrderService", "OrderService", "Sample.Application", "Application"),
            ],
            couplings:
            [
                Coupling("Sample.Application.OrderService", order.Id, "Application", "Domain"),
            ]) with
        {
            ComponentComplexities =
            [
                new ComponentComplexity(
                    order.Id,
                    order.FilePath,
                    MemberCount: 1,
                    MaxCyclomaticComplexity: 25,
                    MaxCognitiveComplexity: 35,
                    TotalCyclomaticComplexity: 25,
                    TotalCognitiveComplexity: 35,
                    MostComplexMember: null),
            ],
        };

        ImpactAnalysis withoutComplexityWeight = ImpactAnalyzer.Analyze(
            report,
            order.Id,
            maxDepth: 3,
            new PrioritizationOptions(10, 15, 0));
        ImpactAnalysis withComplexityWeight = ImpactAnalyzer.Analyze(
            report,
            order.Id,
            maxDepth: 3,
            new PrioritizationOptions(10, 15, 0.30));

        Assert.Equal(
            withoutComplexityWeight.Dependents.Select(ToComparablePath),
            withComplexityWeight.Dependents.Select(ToComparablePath));
        Assert.True(withComplexityWeight.RiskScore > withoutComplexityWeight.RiskScore);
        Assert.Contains(withComplexityWeight.Reasons, reason => reason.Contains("complexity", StringComparison.Ordinal));
    }

    private static AnalysisReport CreateReport(
        IReadOnlyList<Component> components,
        IReadOnlyList<CouplingMetrics> couplings)
    {
        return new AnalysisReport(
            new AnalysisSummary("/tmp/sample", "syntax-only", components.Count, components.Count, couplings.Count, 0, false, false, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.75,
            components,
            [],
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
        string targetProject,
        int line = 1,
        Distance distance = Distance.DifferentProject)
    {
        return new CouplingMetrics(
            source,
            target,
            IntegrationStrength.Functional,
            distance,
            Volatility.Medium,
            sourceProject,
            targetProject,
            Visibility.Public,
            new SourceLocation($"/tmp/{sourceProject}/Source.cs", line));
    }

    private static object ToComparablePath(DependencyPath path)
    {
        return new
        {
            path.Component,
            path.Depth,
            Path = string.Join(" -> ", path.Path),
            path.Project,
            path.Namespace,
            path.CrossesBoundary,
            path.Location,
        };
    }
}
