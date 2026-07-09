using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using DotnetCoupling.Cli;
using DotnetCoupling.Core;
using DotnetCoupling.Git.Volatility;
using DotnetCoupling.Roslyn.Analysis;
using DotnetCoupling.Sarif;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DotnetCoupling.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CliApplication).Assembly,
            typeof(ConfigurationLoader).Assembly,
            typeof(GitVolatility).Assembly,
            typeof(CSharpDependencyAnalyzer).Assembly,
            typeof(SarifReportRenderer).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> CoreTypes = Types().That()
        .ResideInAssembly("DotnetCoupling.Core")
        .As("DotnetCoupling.Core");

    private static readonly IObjectProvider<IType> GitTypes = Types().That()
        .ResideInAssembly("DotnetCoupling.Git")
        .As("DotnetCoupling.Git");

    private static readonly IObjectProvider<IType> RoslynTypes = Types().That()
        .ResideInAssembly("DotnetCoupling.Roslyn")
        .As("DotnetCoupling.Roslyn");

    private static readonly IObjectProvider<IType> SarifTypes = Types().That()
        .ResideInAssembly("DotnetCoupling.Sarif")
        .As("DotnetCoupling.Sarif");

    private static readonly IObjectProvider<IType> CliTypes = Types().That()
        .ResideInAssembly("DotnetCoupling.Cli")
        .As("DotnetCoupling.Cli");

    private static readonly IObjectProvider<IType> MicrosoftCodeAnalysisTypes = Types().That()
        .HaveFullNameContaining("Microsoft.CodeAnalysis.")
        .As("Microsoft.CodeAnalysis");

    private static readonly IObjectProvider<IType> MicrosoftSarifTypes = Types().That()
        .HaveFullNameContaining("Microsoft.CodeAnalysis.Sarif")
        .As("Microsoft.CodeAnalysis.Sarif");

    private static readonly IObjectProvider<IType> SystemCommandLineTypes = Types().That()
        .HaveFullNameContaining("System.CommandLine.")
        .As("System.CommandLine");

    [Fact]
    public void CoreProject_DoesNotDependOnOuterProjects()
    {
        IArchRule rule = Types().That().Are(CoreTypes)
            .Should().NotDependOnAny(CliTypes)
            .AndShould().NotDependOnAny(GitTypes)
            .AndShould().NotDependOnAny(RoslynTypes)
            .AndShould().NotDependOnAny(SarifTypes)
            .AndShould().NotDependOnAny(MicrosoftCodeAnalysisTypes)
            .AndShould().NotDependOnAny(SystemCommandLineTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void GitProject_DoesNotDependOnCliRoslynOrFrameworkOuterLayers()
    {
        IArchRule rule = Types().That().Are(GitTypes)
            .Should().NotDependOnAny(CliTypes)
            .AndShould().NotDependOnAny(RoslynTypes)
            .AndShould().NotDependOnAny(SarifTypes)
            .AndShould().NotDependOnAny(MicrosoftCodeAnalysisTypes)
            .AndShould().NotDependOnAny(SystemCommandLineTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void RoslynProject_DoesNotDependOnCliOrGitProject()
    {
        IArchRule rule = Types().That().Are(RoslynTypes)
            .Should().NotDependOnAny(CliTypes)
            .AndShould().NotDependOnAny(GitTypes)
            .AndShould().NotDependOnAny(SarifTypes)
            .AndShould().NotDependOnAny(SystemCommandLineTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void SarifProject_DependsOnlyOnCoreProject()
    {
        IArchRule rule = Types().That().Are(SarifTypes)
            .Should().NotDependOnAny(CliTypes)
            .AndShould().NotDependOnAny(GitTypes)
            .AndShould().NotDependOnAny(RoslynTypes)
            .AndShould().NotDependOnAny(SystemCommandLineTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void CliProject_DoesNotDependOnMicrosoftCodeAnalysis()
    {
        IArchRule rule = Types().That().Are(CliTypes)
            .Should().NotDependOnAny(MicrosoftCodeAnalysisTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void SarifSdkDependency_IsIsolatedToSarifProject()
    {
        IArchRule coreRule = Types().That().Are(CoreTypes)
            .Should().NotDependOnAny(MicrosoftSarifTypes)
            .WithoutRequiringPositiveResults();
        IArchRule gitRule = Types().That().Are(GitTypes)
            .Should().NotDependOnAny(MicrosoftSarifTypes)
            .WithoutRequiringPositiveResults();
        IArchRule roslynRule = Types().That().Are(RoslynTypes)
            .Should().NotDependOnAny(MicrosoftSarifTypes)
            .WithoutRequiringPositiveResults();

        coreRule.Check(Architecture);
        gitRule.Check(Architecture);
        roslynRule.Check(Architecture);
    }
}
