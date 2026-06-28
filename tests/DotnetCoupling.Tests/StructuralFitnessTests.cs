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
    }
}
