using DotnetCoupling.Core;
using DotnetCoupling.Git;
using DotnetCoupling.Roslyn;
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
    public void DetectIssues_SupportingSubdomainWithHighObservedChurn_AddsAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Reporting/ReportBuilder.cs",
            Subdomain("Reporting", SubdomainCategory.Supporting, "src/Reporting/**", Volatility.Low));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.AccidentalVolatility);
        Assert.Equal(Severity.Medium, issue.Severity);
        Assert.Equal("Sample.Reporting.ReportBuilder", issue.Source);
        Assert.Equal("Reporting", issue.Target);
        Assert.Contains("expected Low", issue.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectIssues_GenericSubdomainWithHighObservedChurn_AddsAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.IdentityProvider.TokenClient");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/IdentityProvider/TokenClient.cs",
            Subdomain("IdentityProvider", SubdomainCategory.Generic, "src/IdentityProvider/**", Volatility.Low));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.AccidentalVolatility);
        Assert.Equal(Severity.Medium, issue.Severity);
        Assert.Equal("Sample.IdentityProvider.TokenClient", issue.Source);
        Assert.Equal("IdentityProvider", issue.Target);
        Assert.Contains("Generic", issue.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectIssues_SupportingSubdomainWithMediumExpectedVolatility_AddsAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Reporting/ReportBuilder.cs",
            Subdomain("Reporting", SubdomainCategory.Supporting, "src/Reporting/**", Volatility.Medium));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.AccidentalVolatility);
        Assert.Equal("Reporting", issue.Target);
        Assert.Contains("expected Medium", issue.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SubdomainCategory.Supporting)]
    [InlineData(SubdomainCategory.Generic)]
    public void DetectIssues_NonCoreSubdomainWithHighExpectedVolatility_DoesNotAddAccidentalVolatility(
        SubdomainCategory category)
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Reporting/ReportBuilder.cs",
            Subdomain("Reporting", category, "src/Reporting/**", Volatility.High));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_CoreSubdomainWithHighObservedChurn_DoesNotAddAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Billing.Invoice");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Billing/Invoice.cs",
            Subdomain("Billing", SubdomainCategory.Core, "src/Billing/**", Volatility.High));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_CoreSubdomainWithLowExpectedVolatility_DoesNotAddAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Billing.Invoice");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Billing/Invoice.cs",
            Subdomain("Billing", SubdomainCategory.Core, "src/Billing/**", Volatility.Low));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_OverlappingDomainPathPatterns_UsesFirstMatchingSubdomain()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Reporting/ReportBuilder.cs",
            Subdomain("AllSource", SubdomainCategory.Supporting, "src/**", Volatility.Low),
            Subdomain("Reporting", SubdomainCategory.Generic, "src/Reporting/**", Volatility.Low));

        CouplingIssue issue = Assert.Single(issues, issue => issue.Type == IssueType.AccidentalVolatility);
        Assert.Equal("AllSource", issue.Target);
        Assert.Contains("Supporting", issue.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectIssues_DomainContextPathDoesNotMatchTarget_DoesNotAddAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            "/repo/src/Reporting/ReportBuilder.cs",
            Subdomain("Billing", SubdomainCategory.Supporting, "src/Billing/**", Volatility.Low));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_HighVolatilityTargetNotParsedAsComponent_DoesNotAddAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Sample.Reporting.ReportBuilder");

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            targetFilePath: null,
            Subdomain("Reporting", SubdomainCategory.Supporting, "src/Reporting/**", Volatility.Low));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_ExternalPackageTargetWithoutParsedComponent_DoesNotAddAccidentalVolatility()
    {
        CouplingMetrics coupling = HighVolatilityCoupling("Newtonsoft.Json", Distance.ExternalPackage);

        List<CouplingIssue> issues = DetectWithDomain(
            coupling,
            targetFilePath: null,
            Subdomain("Reporting", SubdomainCategory.Supporting, "src/Reporting/**", Volatility.Low));

        Assert.DoesNotContain(issues, issue => issue.Type == IssueType.AccidentalVolatility);
    }

    [Fact]
    public void DetectIssues_IntrusiveFarCoupling_AddsInappropriateIntimacy()
    {
        List<CouplingIssue> issues = Detect(Coupling("A.Api.Source", "A.Infrastructure.Target", IntegrationStrength.Intrusive, Distance.DifferentNamespace));

        Assert.Contains(issues, issue => issue.Type == IssueType.InappropriateIntimacy && issue.Severity == Severity.High);
    }

    [Fact]
    public void DetectIssues_TestProjectSource_SkipsSourceDrivenIssues()
    {
        AnalysisOptions options = AnalysisOptions.Default with
        {
            TestProjectPathPatterns = ["**/tests/**"],
        };
        CouplingMetrics testCoupling = Coupling(
            "Sample.Tests.HandlerTests",
            "Sample.Core.Handler",
            IntegrationStrength.Intrusive,
            Distance.DifferentProject) with
        {
            Location = new SourceLocation("/repo/tests/Sample.Tests/HandlerTests.cs", 1),
        };
        CouplingMetrics productionCoupling = Coupling(
            "Sample.Api.Handler",
            "Sample.Infrastructure.Repository",
            IntegrationStrength.Functional,
            Distance.DifferentProject) with
        {
            Location = new SourceLocation("/repo/src/Sample.Api/Handler.cs", 1),
        };

        List<CouplingIssue> issues = IssueDetector.DetectIssues(
            [CouplingScoring.Calculate(testCoupling), CouplingScoring.Calculate(productionCoupling)],
            [],
            new Dictionary<string, Component>(StringComparer.Ordinal),
            options);

        Assert.DoesNotContain(issues, issue => issue.Source == "Sample.Tests.HandlerTests");
        Assert.Contains(issues, issue =>
            issue.Type == IssueType.GlobalComplexity
            && issue.Source == "Sample.Api.Handler"
            && issue.Target == "Sample.Infrastructure.Repository");
    }

    [Fact]
    public void DetectIssues_TestProjectTemporalCoupling_SkipsHiddenCoupling()
    {
        AnalysisOptions options = AnalysisOptions.Default with
        {
            TestProjectPathPatterns = ["**/tests/**"],
        };

        List<CouplingIssue> issues = IssueDetector.DetectIssues(
            [],
            [
                new TemporalCoupling("/repo/tests/Sample.Tests/HandlerTests.cs", "/repo/src/Sample.Api/Handler.cs", 3),
                new TemporalCoupling("/repo/src/Sample.Api/Controller.cs", "/repo/src/Sample.Api/Handler.cs", 3),
            ],
            new Dictionary<string, Component>(StringComparer.Ordinal),
            options);

        Assert.DoesNotContain(issues, issue => issue.Source.Contains("/tests/", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Type == IssueType.HiddenCoupling
            && issue.Source == "/repo/src/Sample.Api/Controller.cs"
            && issue.Target == "/repo/src/Sample.Api/Handler.cs");
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

    private static CouplingMetrics HighVolatilityCoupling(
        string target,
        Distance distance = Distance.DifferentNamespace)
    {
        return Coupling("Sample.Api.Handler", target, IntegrationStrength.Model, distance, Volatility.High);
    }

    private static List<CouplingIssue> DetectWithDomain(
        CouplingMetrics coupling,
        string? targetFilePath,
        params DomainSubdomain[] subdomains)
    {
        Dictionary<string, Component> componentsById = new(StringComparer.Ordinal);
        if (targetFilePath is not null)
        {
            componentsById[coupling.Target] = Component(coupling.Target, targetFilePath);
        }

        AnalysisOptions options = AnalysisOptions.Default with
        {
            DomainContext = new DomainContext(subdomains),
        };

        return IssueDetector.DetectIssues(
            [CouplingScoring.Calculate(coupling)],
            [],
            componentsById,
            options);
    }

    private static DomainSubdomain Subdomain(
        string name,
        SubdomainCategory category,
        string pathPattern,
        Volatility expectedVolatility)
    {
        return new DomainSubdomain(name, category, [pathPattern], expectedVolatility);
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
