using DotnetCoupling.Core;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CalibrationEvidenceTests
{
    [Fact]
    public void DetectIssues_AdapterToCoreRules_RemainsHighPriority()
    {
        CouplingMetrics observed = Coupling(
            "Sample.Roslyn.Adapter",
            "Sample.Core.IssueDetector",
            IntegrationStrength.Functional,
            Volatility.Low);
        Component source = Component(observed.Source, "/repo/src/Roslyn/Adapter.cs");
        Component target = Component(observed.Target, "/repo/src/Core/IssueDetector.cs");
        DomainContext domainContext = new(
            [new DomainSubdomain("CoreRules", SubdomainCategory.Core, ["src/Core/IssueDetector.cs"], Volatility.High)],
            [
                new DomainArea("RoslynAdapter", ["src/Roslyn/**"], TechnicalRole.Adapter),
                new DomainArea("CoreRules", ["src/Core/IssueDetector.cs"], TechnicalRole.DomainModel),
            ]);

        List<CouplingIssue> issues = Detect([observed], Components(source, target), domainContext);

        Assert.Contains(issues, issue =>
            issue.Type == IssueType.GlobalComplexity
            && issue.Severity == Severity.High);
        Assert.Contains(issues, issue =>
            issue.Type == IssueType.CascadingChangeRisk
            && issue.Severity == Severity.High);
    }

    [Fact]
    public void DetectIssues_StableReportingTarget_DowngradesGlobalComplexityToMedium()
    {
        CouplingMetrics observed = Coupling(
            "Sample.Cli.OutputAdapter",
            "Sample.Core.ReportRenderer",
            IntegrationStrength.Functional,
            Volatility.Low);
        Component source = Component(observed.Source, "/repo/src/Cli/OutputAdapter.cs");
        Component target = Component(observed.Target, "/repo/src/Core/ReportRenderer.cs");
        DomainContext domainContext = new(
            [new DomainSubdomain("CoreReporting", SubdomainCategory.Supporting, ["src/Core/ReportRenderer.cs"], Volatility.Medium)],
            [
                new DomainArea("CliOutputAdapter", ["src/Cli/**"], TechnicalRole.Adapter),
                new DomainArea("CoreReporting", ["src/Core/ReportRenderer.cs"], TechnicalRole.Adapter),
            ]);

        CouplingIssue issue = Assert.Single(Detect([observed], Components(source, target), domainContext));

        Assert.Equal(IssueType.GlobalComplexity, issue.Type);
        Assert.Equal(Severity.Medium, issue.Severity);
    }

    [Fact]
    public void DetectIssues_ContractTarget_RemovesGlobalComplexity()
    {
        CouplingMetrics observed = Coupling(
            "Sample.Cli.Writer",
            "Sample.Core.AnalysisReport",
            IntegrationStrength.Functional,
            Volatility.Medium);
        Component source = Component(observed.Source, "/repo/src/Cli/Writer.cs");
        Component target = Component(observed.Target, "/repo/src/Core/Models.cs");
        DomainContext domainContext = new(
            [new DomainSubdomain("CoreContracts", SubdomainCategory.Supporting, ["src/Core/Models.cs"], Volatility.Medium)],
            [new DomainArea("CoreContracts", ["src/Core/Models.cs"], TechnicalRole.Contract)]);

        List<CouplingIssue> issues = Detect([observed], Components(source, target), domainContext);

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.GlobalComplexity);
    }

    [Fact]
    public void DetectIssues_CompositionRootSource_RemovesGlobalComplexity()
    {
        CouplingMetrics observed = Coupling(
            "Sample.Cli.CliApplication",
            "Sample.Roslyn.CSharpDependencyAnalyzer",
            IntegrationStrength.Functional,
            Volatility.Medium);
        Component source = Component(observed.Source, "/repo/src/Cli/CliApplication.cs");
        Component target = Component(observed.Target, "/repo/src/Roslyn/CSharpDependencyAnalyzer.cs");
        DomainContext domainContext = new(
            [new DomainSubdomain("Roslyn", SubdomainCategory.Supporting, ["src/Roslyn/**"], Volatility.Medium)],
            [new DomainArea("CompositionRoot", ["src/Cli/CliApplication.cs"], TechnicalRole.CompositionRoot)]);

        List<CouplingIssue> issues = Detect([observed], Components(source, target), domainContext);

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.GlobalComplexity);
    }

    private static List<CouplingIssue> Detect(
        IReadOnlyCollection<CouplingMetrics> observedCouplings,
        IReadOnlyDictionary<string, Component> componentsById,
        DomainContext domainContext)
    {
        List<CouplingMetrics> effectiveCouplings = CouplingCalibrator.Calibrate(observedCouplings, componentsById, domainContext);
        AnalysisOptions options = AnalysisOptions.Default with
        {
            DomainContext = domainContext,
        };

        return IssueDetector.DetectIssuesWithSuppression(
            effectiveCouplings.Select(CouplingScoring.Calculate).ToArray(),
            [],
            componentsById,
            options,
            observedCouplings).Issues;
    }

    private static CouplingMetrics Coupling(
        string source,
        string target,
        IntegrationStrength strength,
        Volatility volatility)
    {
        return new CouplingMetrics(
            source,
            target,
            strength,
            Distance.DifferentProject,
            volatility,
            "Sample.Source",
            "Sample.Target",
            Visibility.Public,
            new SourceLocation("/repo/src/Source.cs", 1));
    }

    private static Component Component(string id, string filePath)
    {
        return new Component(
            id,
            id.Split('.').Last(),
            id[..id.LastIndexOf('.')],
            null,
            filePath,
            ComponentKind.Class,
            Visibility.Public);
    }

    private static Dictionary<string, Component> Components(params Component[] components)
    {
        return components.ToDictionary(component => component.Id, StringComparer.Ordinal);
    }
}
