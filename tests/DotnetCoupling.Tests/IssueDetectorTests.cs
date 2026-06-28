using DotnetCoupling.Cli.Analysis;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class IssueDetectorTests
{
    [Fact]
    public void DetectIssues_StrongFarCoupling_AddsGlobalComplexity()
    {
        List<CouplingIssue> issues = Detect(Coupling("A.Api.Source", "A.Infrastructure.Target", IntegrationStrength.Functional, Distance.DifferentNamespace));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.GlobalComplexity);
        Assert.Equal(Severity.Medium, issue.Severity);
    }

    [Fact]
    public void DetectIssues_StrongHighVolatility_AddsCascadingChangeRisk()
    {
        List<CouplingIssue> issues = Detect(Coupling("A.Source", "A.Target", IntegrationStrength.Functional, Distance.SameNamespace, Volatility.High));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.CascadingChangeRisk);
        Assert.Equal(Severity.High, issue.Severity);
    }

    [Fact]
    public void DetectIssues_IntrusiveFarCoupling_AddsInappropriateIntimacy()
    {
        List<CouplingIssue> issues = Detect(Coupling("A.Api.Source", "A.Infrastructure.Target", IntegrationStrength.Intrusive, Distance.DifferentNamespace));

        Assert.Contains(issues, issue => issue.Type == IssueType.InappropriateIntimacy && issue.Severity == Severity.High);
    }

    [Fact]
    public void DetectIssues_DuplicateIssueKeys_ReturnsSingleIssue()
    {
        CouplingMetrics first = Coupling("A.Api.Source", "A.Infrastructure.Target", IntegrationStrength.Functional, Distance.DifferentNamespace);
        CouplingMetrics second = first with { Location = new SourceLocation("/repo/Source.cs", 2) };

        List<CouplingIssue> issues = IssueDetector.DetectIssues(
            [CouplingScoring.Calculate(first), CouplingScoring.Calculate(second)],
            [],
            new Dictionary<string, Component>(StringComparer.Ordinal));

        Assert.Single(issues, issue =>
            issue.Type == IssueType.GlobalComplexity
            && issue.Source == "A.Api.Source"
            && issue.Target == "A.Infrastructure.Target");
    }

    [Fact]
    public void AddFanInFanOutIssues_ThresholdBoundaries_AreExclusive()
    {
        List<CouplingIssue> issues = [];
        IssueDetector.AddFanInFanOutIssues(
            Enumerable.Range(0, IssueDetector.MaxDependencies)
                .Select(index => Coupling("A.Source", $"A.Target{index}", IntegrationStrength.Model, Distance.SameNamespace)),
            issues);

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.HighEfferentCoupling);

        IssueDetector.AddFanInFanOutIssues(
            Enumerable.Range(0, IssueDetector.MaxDependencies + 1)
                .Select(index => Coupling("B.Source", $"B.Target{index}", IntegrationStrength.Model, Distance.SameNamespace)),
            issues);

        Assert.Contains(issues, issue => issue.Type == IssueType.HighEfferentCoupling);
    }

    [Fact]
    public void AddCircularDependencyIssues_NamespaceCycle_AddsCircularDependency()
    {
        List<CouplingIssue> issues = [];

        IssueDetector.AddCircularDependencyIssues(
            [
                Coupling("A.One.Source", "A.Two.Target", IntegrationStrength.Model, Distance.DifferentNamespace),
                Coupling("A.Two.Source", "A.One.Target", IntegrationStrength.Model, Distance.DifferentNamespace),
            ],
            issues);

        Assert.Contains(issues, issue => issue.Type == IssueType.CircularDependency);
    }

    [Fact]
    public void AddHiddenCouplingIssues_ExplicitFileDependency_SuppressesHiddenCoupling()
    {
        Component source = Component("A.Source", "/repo/Source.cs");
        Component target = Component("A.Target", "/repo/Target.cs");
        Dictionary<string, Component> componentsById = new(StringComparer.Ordinal)
        {
            [source.Id] = source,
            [target.Id] = target,
        };
        List<CouplingIssue> issues = [];

        IssueDetector.AddHiddenCouplingIssues(
            [new TemporalCoupling(source.FilePath, target.FilePath, 3)],
            [Coupling(source.Id, target.Id, IntegrationStrength.Model, Distance.SameNamespace)],
            componentsById,
            issues);

        Assert.Empty(issues);
    }

    [Fact]
    public void AddHiddenCouplingIssues_NoExplicitDependency_AddsHiddenCouplingWithSeverityBoundary()
    {
        List<CouplingIssue> issues = [];

        IssueDetector.AddHiddenCouplingIssues(
            [
                new TemporalCoupling("/repo/A.cs", "/repo/B.cs", 3),
                new TemporalCoupling("/repo/C.cs", "/repo/D.cs", IssueDetector.HiddenCouplingHighThreshold),
            ],
            [],
            new Dictionary<string, Component>(StringComparer.Ordinal),
            issues);

        Assert.Contains(issues, issue => issue.Type == IssueType.HiddenCoupling && issue.Severity == Severity.Medium);
        Assert.Contains(issues, issue => issue.Type == IssueType.HiddenCoupling && issue.Severity == Severity.High);
    }

    [Fact]
    public void AddScatteredExternalCouplingIssues_UsesExternalOnlyAndBreadthThreshold()
    {
        List<CouplingIssue> issues = [];
        List<CouplingMetrics> couplings = Enumerable.Range(0, IssueDetector.ScatteredExternalBreadth - 1)
            .Select(index => Coupling($"A.Source{index}", "Newtonsoft.Json", IntegrationStrength.Contract, Distance.ExternalPackage))
            .Append(Coupling("A.Internal", "A.Target", IntegrationStrength.Functional, Distance.DifferentNamespace))
            .ToList();

        IssueDetector.AddScatteredExternalCouplingIssues(couplings, issues);
        Assert.Empty(issues);

        couplings.Add(Coupling("A.SourceThreshold", "Newtonsoft.Json", IntegrationStrength.Contract, Distance.ExternalPackage));
        IssueDetector.AddScatteredExternalCouplingIssues(couplings, issues);

        Assert.Contains(issues, issue => issue.Type == IssueType.ScatteredExternalCoupling && issue.Target == "Newtonsoft.Json");
    }

    private static List<CouplingIssue> Detect(CouplingMetrics coupling)
    {
        return IssueDetector.DetectIssues(
            [CouplingScoring.Calculate(coupling)],
            [],
            new Dictionary<string, Component>(StringComparer.Ordinal));
    }

    private static CouplingMetrics Coupling(
        string source,
        string target,
        IntegrationStrength strength,
        Distance distance,
        Volatility volatility = Volatility.Low)
    {
        return new CouplingMetrics(
            source,
            target,
            strength,
            distance,
            volatility,
            null,
            null,
            Visibility.Public,
            new SourceLocation("/repo/Source.cs", 1));
    }

    private static Component Component(string id, string filePath)
    {
        string name = id.Split('.').Last();
        string namespaceName = id[..id.LastIndexOf('.')];
        return new Component(id, name, namespaceName, null, filePath, ComponentKind.Class, Visibility.Public);
    }
}
