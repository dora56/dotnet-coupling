using DotnetCoupling.Core;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CouplingCalibratorTests
{
    [Theory]
    [InlineData(IntegrationStrength.Functional)]
    [InlineData(IntegrationStrength.Intrusive)]
    public void Calibrate_TargetContractRole_LowersStrongCouplingToModel(IntegrationStrength strength)
    {
        CouplingMetrics coupling = Coupling(strength, Volatility.High);
        Component source = Component(coupling.Source, "/repo/src/App/Handler.cs");
        Component target = Component(coupling.Target, "/repo/src/Core/Models.cs");
        DomainContext domainContext = new(
            [],
            [new DomainArea("Contracts", ["src/Core/**"], TechnicalRole.Contract)]);

        CouplingMetrics calibrated = Assert.Single(CouplingCalibrator.Calibrate(
            [coupling],
            Components(source, target),
            domainContext));

        Assert.Equal(IntegrationStrength.Model, calibrated.Strength);
        Assert.Equal(Volatility.High, calibrated.Volatility);
    }

    [Fact]
    public void Calibrate_CompositionRootSource_LowersCrossBoundaryOrchestrationToModel()
    {
        CouplingMetrics coupling = Coupling(IntegrationStrength.Functional, Volatility.Medium);
        Component source = Component(coupling.Source, "/repo/src/Cli/CliApplication.cs");
        Component target = Component(coupling.Target, "/repo/src/Roslyn/CSharpDependencyAnalyzer.cs");
        DomainContext domainContext = new(
            [],
            [new DomainArea("Composition", ["src/Cli/CliApplication.cs"], TechnicalRole.CompositionRoot)]);

        CouplingMetrics calibrated = Assert.Single(CouplingCalibrator.Calibrate(
            [coupling],
            Components(source, target),
            domainContext));

        Assert.Equal(IntegrationStrength.Model, calibrated.Strength);
        Assert.Equal(Volatility.Medium, calibrated.Volatility);
    }

    [Fact]
    public void Calibrate_TargetSubdomainExpectedVolatility_OverridesObservedVolatility()
    {
        CouplingMetrics coupling = Coupling(IntegrationStrength.Functional, Volatility.High);
        Component source = Component(coupling.Source, "/repo/src/App/Handler.cs");
        Component target = Component(coupling.Target, "/repo/src/Reporting/ReportBuilder.cs");
        DomainContext domainContext = new(
            [new DomainSubdomain("Reporting", SubdomainCategory.Supporting, ["src/Reporting/**"], Volatility.Low)],
            []);

        CouplingMetrics calibrated = Assert.Single(CouplingCalibrator.Calibrate(
            [coupling],
            Components(source, target),
            domainContext));

        Assert.Equal(IntegrationStrength.Functional, calibrated.Strength);
        Assert.Equal(Volatility.Low, calibrated.Volatility);
    }

    [Fact]
    public void Calibrate_OverlappingAreas_UsesFirstMatchingArea()
    {
        CouplingMetrics coupling = Coupling(IntegrationStrength.Functional, Volatility.Low);
        Component source = Component(coupling.Source, "/repo/src/App/Handler.cs");
        Component target = Component(coupling.Target, "/repo/src/Core/Models.cs");
        DomainContext domainContext = new(
            [],
            [
                new DomainArea("AllSource", ["src/**"], TechnicalRole.Adapter),
                new DomainArea("Contracts", ["src/Core/**"], TechnicalRole.Contract),
            ]);

        CouplingMetrics calibrated = Assert.Single(CouplingCalibrator.Calibrate(
            [coupling],
            Components(source, target),
            domainContext));

        Assert.Equal(IntegrationStrength.Functional, calibrated.Strength);
    }

    [Fact]
    public void Calibrate_NoDomainContext_ReturnsObservedCoupling()
    {
        CouplingMetrics coupling = Coupling(IntegrationStrength.Functional, Volatility.High);

        CouplingMetrics calibrated = Assert.Single(CouplingCalibrator.Calibrate(
            [coupling],
            new Dictionary<string, Component>(StringComparer.Ordinal),
            DomainContext.Empty));

        Assert.Equal(coupling, calibrated);
    }

    private static CouplingMetrics Coupling(IntegrationStrength strength, Volatility volatility)
    {
        return new CouplingMetrics(
            "Sample.App.Handler",
            "Sample.Core.Target",
            strength,
            Distance.DifferentProject,
            volatility,
            "Sample.App",
            "Sample.Core",
            Visibility.Public,
            new SourceLocation("/repo/src/App/Handler.cs", 1));
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
