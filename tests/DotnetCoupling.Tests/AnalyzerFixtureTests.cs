using DotnetCoupling.Core;
using DotnetCoupling.Git;
using DotnetCoupling.Roslyn;
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

    [Fact]
    public void Analyze_CsprojInput_AssignsProjectNameToComponentsAndCouplings()
    {
        string projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "Sample.App.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample.App</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Handler.cs"),
            """
            namespace Sample.App;

            public sealed class Handler
            {
                public Repository Repository { get; } = new();
            }
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Repository.cs"),
            """
            namespace Sample.App;

            public sealed class Repository
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(projectPath, useGit: false, gitMonths: 6);

        Assert.Equal(2, report.Components.Count);
        Assert.All(report.Components, component => Assert.Equal("Sample.App", component.ProjectName));
        CouplingMetrics coupling = Assert.Single(report.Couplings);
        Assert.Equal("Sample.App", coupling.SourceProject);
        Assert.Equal("Sample.App", coupling.TargetProject);
    }

    [Fact]
    public void Analyze_SlnxInput_UsesProjectBoundaryForDistance()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string domain = Path.Combine(root, "Domain");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(domain);
        File.WriteAllText(
            Path.Combine(root, "Sample.slnx"),
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Domain/Domain.csproj" />
            </Solution>
            """);
        File.WriteAllText(
            Path.Combine(app, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Domain/Domain.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(domain, "Domain.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(app, "Handler.cs"),
            """
            namespace Sample.App;

            public sealed class Handler
            {
                public Entity Entity { get; } = new();
            }
            """);
        File.WriteAllText(
            Path.Combine(domain, "Entity.cs"),
            """
            namespace Sample.Domain;

            public sealed class Entity
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(Path.Combine(root, "Sample.slnx"), useGit: false, gitMonths: 6);

        Assert.Equal(2, report.Components.Count);
        CouplingMetrics coupling = Assert.Single(report.Couplings);
        Assert.Equal("App", coupling.SourceProject);
        Assert.Equal("Domain", coupling.TargetProject);
        Assert.Equal(Distance.DifferentProject, coupling.Distance);
    }

    [Fact]
    public void Analyze_SlnInput_LoadsReferencedProjects()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        Directory.CreateDirectory(app);
        File.WriteAllText(
            Path.Combine(root, "Sample.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);
        File.WriteAllText(
            Path.Combine(app, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(app, "Handler.cs"),
            """
            namespace Sample.App;

            public sealed class Handler
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(Path.Combine(root, "Sample.sln"), useGit: false, gitMonths: 6);

        Component component = Assert.Single(report.Components);
        Assert.Equal("App", component.ProjectName);
    }
}
