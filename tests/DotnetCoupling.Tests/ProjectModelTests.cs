using DotnetCoupling.Core;
using DotnetCoupling.Roslyn;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class ProjectModelTests
{
    [Fact]
    public void Load_SlnxInput_ResolvesProjectReferencesIntoGraph()
    {
        string root = CreateDirectory();
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

        ProjectModel model = ProjectModel.Load(Path.Combine(root, "Sample.slnx"), AnalysisOptions.Default);

        ProjectModelProject appProject = Assert.Single(model.Projects, project => project.ProjectName == "App");
        ProjectModelReference reference = Assert.Single(appProject.ProjectReferences);
        Assert.Equal("Domain", reference.TargetProjectName);
    }

    [Fact]
    public void Load_SlnInput_ResolvesProjectReferencesIntoGraph()
    {
        string root = CreateDirectory();
        string app = Path.Combine(root, "App");
        string domain = Path.Combine(root, "Domain");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(domain);

        File.WriteAllText(
            Path.Combine(root, "Sample.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Domain", "Domain\Domain.csproj", "{22222222-2222-2222-2222-222222222222}"
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

        ProjectModel model = ProjectModel.Load(Path.Combine(root, "Sample.sln"), AnalysisOptions.Default);

        ProjectModelProject appProject = Assert.Single(model.Projects, project => project.ProjectName == "App");
        ProjectModelReference reference = Assert.Single(appProject.ProjectReferences);
        Assert.Equal("Domain", reference.TargetProjectName);
    }

    [Fact]
    public void Load_CsprojInput_ReadsAssemblyAndPackageMetadata()
    {
        string root = CreateDirectory();
        string projectPath = Path.Combine(root, "App.csproj");

        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample.App.Assembly</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
                <PackageReference Include="Spectre.Console" Version="0.49.1" />
              </ItemGroup>
            </Project>
            """);

        ProjectModel model = ProjectModel.Load(projectPath, AnalysisOptions.Default);

        ProjectModelProject project = Assert.Single(model.Projects);
        Assert.Equal("Sample.App.Assembly", project.AssemblyName);
        Assert.Collection(
            project.PackageReferences.OrderBy(reference => reference.PackageId, StringComparer.Ordinal),
            reference =>
            {
                Assert.Equal("Microsoft.Extensions.DependencyInjection", reference.PackageId);
                Assert.Equal("10.0.0", reference.Version);
            },
            reference =>
            {
                Assert.Equal("Spectre.Console", reference.PackageId);
                Assert.Equal("0.49.1", reference.Version);
            });
    }

    [Fact]
    public void Load_SlnxInput_MissingProjectReference_EmitsRecoverableDiagnostic()
    {
        string root = CreateDirectory();
        string app = Path.Combine(root, "App");
        Directory.CreateDirectory(app);

        File.WriteAllText(
            Path.Combine(root, "Sample.slnx"),
            """
            <Solution>
              <Project Path="App/App.csproj" />
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
                <ProjectReference Include="../Missing/Missing.csproj" />
              </ItemGroup>
            </Project>
            """);

        ProjectModel model = ProjectModel.Load(Path.Combine(root, "Sample.slnx"), AnalysisOptions.Default);

        ProjectModelDiagnostic diagnostic = Assert.Single(model.Diagnostics);
        Assert.Equal("missing-project-reference", diagnostic.Code);
        Assert.Contains("Missing.csproj", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SlnxInput_InvalidProjectFile_EmitsRecoverableDiagnosticAndContinues()
    {
        string root = CreateDirectory();
        string app = Path.Combine(root, "App");
        string broken = Path.Combine(root, "Broken");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(broken);

        File.WriteAllText(
            Path.Combine(root, "Sample.slnx"),
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Broken/Broken.csproj" />
            </Solution>
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
        File.WriteAllText(Path.Combine(broken, "Broken.csproj"), "<Project><PropertyGroup>");

        ProjectModel model = ProjectModel.Load(Path.Combine(root, "Sample.slnx"), AnalysisOptions.Default);

        ProjectModelProject project = Assert.Single(model.Projects);
        Assert.Equal("App", project.ProjectName);
        ProjectModelDiagnostic diagnostic = Assert.Single(model.Diagnostics);
        Assert.Equal("invalid-project", diagnostic.Code);
        Assert.Contains("Broken.csproj", diagnostic.Message, StringComparison.Ordinal);
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
