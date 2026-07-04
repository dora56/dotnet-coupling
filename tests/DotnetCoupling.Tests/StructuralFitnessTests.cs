using System.Xml.Linq;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class StructuralFitnessTests
{
    [Fact]
    public void CliProject_DependsOnFeatureProjectsInsteadOfRoslynPackage()
    {
        string cliProjectPath = Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Cli", "DotnetCoupling.Cli.csproj");
        XDocument document = XDocument.Load(cliProjectPath);

        string[] packageReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
        string[] projectReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();

        Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp", packageReferences);
        Assert.Contains(projectReferences, reference => reference.Contains("DotnetCoupling.Core", StringComparison.Ordinal));
        Assert.Contains(projectReferences, reference => reference.Contains("DotnetCoupling.Git", StringComparison.Ordinal));
        Assert.Contains(projectReferences, reference => reference.Contains("DotnetCoupling.Roslyn", StringComparison.Ordinal));
        Assert.Contains(projectReferences, reference => reference.Contains("DotnetCoupling.Sarif", StringComparison.Ordinal));
    }

    [Fact]
    public void RoslynProject_DoesNotDependOnGitProject()
    {
        string roslynProjectPath = Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Roslyn", "DotnetCoupling.Roslyn.csproj");
        XDocument document = XDocument.Load(roslynProjectPath);

        string[] projectReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();

        Assert.DoesNotContain(projectReferences, reference => reference.Contains("DotnetCoupling.Git", StringComparison.Ordinal));
    }

    [Fact]
    public void SarifProject_ContainsOnlySarifSdkProductionPackageReference()
    {
        string sarifProjectPath = Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Sarif", "DotnetCoupling.Sarif.csproj");
        XDocument document = XDocument.Load(sarifProjectPath);

        string[] packageReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
        string[] projectReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();

        Assert.Equal(["Sarif.Sdk"], packageReferences);
        Assert.Contains(projectReferences, reference => reference.Contains("DotnetCoupling.Core", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("DotnetCoupling.Roslyn", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("DotnetCoupling.Git", StringComparison.Ordinal));
    }

    [Fact]
    public void TomlynProductionDependency_IsIsolatedToCoreProject()
    {
        string[] projectPaths =
        [
            Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Cli", "DotnetCoupling.Cli.csproj"),
            Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Core", "DotnetCoupling.Core.csproj"),
            Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Git", "DotnetCoupling.Git.csproj"),
            Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Roslyn", "DotnetCoupling.Roslyn.csproj"),
            Path.Combine(TestPaths.RepositoryRoot, "src", "DotnetCoupling.Sarif", "DotnetCoupling.Sarif.csproj"),
        ];

        Dictionary<string, string[]> packageReferencesByProject = projectPaths.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path)!,
            path => XDocument.Load(path)
                .Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .Select(value => value!)
                .ToArray());

        Assert.Contains("Tomlyn", packageReferencesByProject["DotnetCoupling.Core"]);
        Assert.DoesNotContain("Tomlyn", packageReferencesByProject["DotnetCoupling.Cli"]);
        Assert.DoesNotContain("Tomlyn", packageReferencesByProject["DotnetCoupling.Git"]);
        Assert.DoesNotContain("Tomlyn", packageReferencesByProject["DotnetCoupling.Roslyn"]);
        Assert.DoesNotContain("Tomlyn", packageReferencesByProject["DotnetCoupling.Sarif"]);
    }
}
