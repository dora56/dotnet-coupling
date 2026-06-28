using DotnetCoupling.Core;
using DotnetCoupling.Git;
using DotnetCoupling.Roslyn;
using System.Text.Json;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class ReportRendererTests
{
    [Fact]
    public void Render_SummaryOutput_MatchesGoldenFile()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        string actual = TestPaths.NormalizeFixturePath(ReportRenderer.Render(report, ReportFormat.Summary), fixture);
        string expected = File.ReadAllText(TestPaths.Golden("global-complexity-summary.txt")).TrimEnd();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Render_JsonOutput_MatchesGoldenFileIgnoringPropertyOrder()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        string actual = TestPaths.NormalizeFixturePath(ReportRenderer.Render(report, ReportFormat.Json), fixture);
        string expected = File.ReadAllText(TestPaths.Golden("global-complexity.json"));

        Assert.True(JsonElementDeepEquals(Parse(expected), Parse(actual)));
    }

    [Fact]
    public void Render_JsonOutput_SatisfiesReportSchemaContract()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.1.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.1", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("dotnet-coupling", document.RootElement.GetProperty("tool").GetString());
        Assert.Equal("issue-density", document.RootElement.GetProperty("grade").GetProperty("basis").GetString());
        Assert.Equal("GlobalComplexity", document.RootElement.GetProperty("issues")[0].GetProperty("type").GetString());
        Assert.Equal("Medium", document.RootElement.GetProperty("issues")[0].GetProperty("severity").GetString());
        Assert.Equal("syntax-only", document.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal("syntax-only", document.RootElement.GetProperty("manifest").GetProperty("confidence").GetString());
    }

    [Fact]
    public void Render_JsonOutputWithBaseline_SatisfiesReportSchemaContract()
    {
        CouplingIssue issue = new(
            IssueType.GlobalComplexity,
            Severity.Medium,
            "Sample.Api.Handler",
            "Sample.Infrastructure.Repository",
            0.50,
            "Problem",
            "Recommendation",
            new SourceLocation("/tmp/sample/Api.cs", 1));
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, false, false, 6),
            new GradeResult("C", "Needs attention", "issue-density", "Test"),
            0.50,
            [],
            [],
            [],
            [issue],
            [],
            new BaselineComparison("main", [issue], [], []));
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.2.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.2", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement baseline = document.RootElement.GetProperty("baseline");
        Assert.Equal("main", baseline.GetProperty("ref").GetString());
        Assert.Equal(1, baseline.GetProperty("newIssues").GetArrayLength());
    }

    [Fact]
    public void Render_SummaryOutput_IncludesSGradeWarning()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 1, 20, 20, 0, false, false, 6),
            new GradeResult("S", "Over-optimized warning", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            []);

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.Contains("Grade: S (Over-optimized warning)", rendered);
        Assert.Contains("This is not a trophy.", rendered);
    }

    [Fact]
    public void Render_SummaryOutput_SyntaxOnly_OmitsModeLine()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.DoesNotContain("Mode:", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_JsonOutput_IncludesRecoverableDiagnosticsInManifest()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 1, 1, 0, 0, false, false, 6),
            new GradeResult("A", "Well-balanced", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            [],
            Diagnostics:
            [
                new AnalysisDiagnostic(
                    "missing-project-reference",
                    "Warning",
                    "Referenced project was not found: /tmp/missing/Missing.csproj",
                    "/tmp/sample/App.csproj"),
            ]);

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.1.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("missing-project-reference", diagnostic.GetProperty("code").GetString());
        Assert.Equal("Warning", diagnostic.GetProperty("severity").GetString());
    }

    [Fact]
    public void Render_SummaryOutput_IncludesModeLineForSemanticPreview()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample/App.csproj", "semantic-preview", 1, 1, 0, 0, false, false, 6),
            new GradeResult("A", "Well-balanced", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            []);

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.Contains("Mode: semantic-preview", rendered);
    }

    [Fact]
    public void Render_JsonOutput_UsesSemanticPreviewRunNotesWhenModeIsSemanticPreview()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample/App.csproj", "semantic-preview", 1, 1, 0, 0, false, false, 6),
            new GradeResult("A", "Well-balanced", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            []);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement manifest = document.RootElement.GetProperty("manifest");
        Assert.Equal("semantic-preview", manifest.GetProperty("confidence").GetString());
        string[] runNotes = manifest.GetProperty("runNotes").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Contains("Semantic mode uses MSBuildWorkspace preview loading.", runNotes);
    }

    [Fact]
    public void Render_SummaryOutput_SyntaxAndSemanticPreviewMatchGoldenDiff()
    {
        string projectPath = CreateSemanticDiffProjectFixture();

        AnalysisReport syntaxReport = CSharpDependencyAnalyzer.Analyze(projectPath, AnalysisMode.Syntax, volatilityProvider: null, gitMonths: 6);
        AnalysisReport semanticReport = CSharpDependencyAnalyzer.Analyze(projectPath, AnalysisMode.Semantic, volatilityProvider: null, gitMonths: 6);

        string syntaxActual = ReportRenderer.Render(syntaxReport, ReportFormat.Summary).TrimEnd();
        string semanticActual = ReportRenderer.Render(semanticReport, ReportFormat.Summary).TrimEnd();
        string syntaxExpected = File.ReadAllText(TestPaths.Golden("semantic-diff-syntax-summary.txt")).TrimEnd();
        string semanticExpected = File.ReadAllText(TestPaths.Golden("semantic-diff-semantic-summary.txt")).TrimEnd();

        Assert.Equal(syntaxExpected, syntaxActual);
        Assert.Equal(semanticExpected, semanticActual);
    }

    [Fact]
    public void Render_JsonOutput_CsprojInput_IncludesProjectMetadata()
    {
        string projectPath = CreateProjectMetadataFixture();
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(projectPath, useGit: false, gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("Sample.App.Assembly", project.GetProperty("projectName").GetString());
        Assert.Equal("Sample.App.Assembly", project.GetProperty("assemblyName").GetString());
        Assert.Equal(2, project.GetProperty("sourceFileCount").GetInt32());
        string[] packageReferences = project.GetProperty("packageReferences").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Contains("Spectre.Console", packageReferences);
    }

    [Fact]
    public void Render_JsonOutput_SemanticMode_IncludesProjectMetadata()
    {
        string projectPath = CreateProjectMetadataFixture();
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(projectPath, AnalysisMode.Semantic, volatilityProvider: null, gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("Sample.App.Assembly", project.GetProperty("projectName").GetString());
        Assert.Equal("Sample.App.Assembly", project.GetProperty("assemblyName").GetString());
        Assert.Equal(2, project.GetProperty("sourceFileCount").GetInt32());
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static bool JsonElementDeepEquals(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        return expected.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(expected, actual),
            JsonValueKind.Array => expected.GetArrayLength() == actual.GetArrayLength()
                && expected.EnumerateArray().Zip(actual.EnumerateArray()).All(pair => JsonElementDeepEquals(pair.First, pair.Second)),
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.Number => expected.GetRawText() == actual.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => expected.GetBoolean() == actual.GetBoolean(),
            JsonValueKind.Null => true,
            _ => expected.GetRawText() == actual.GetRawText(),
        };
    }

    private static bool ObjectEquals(JsonElement expected, JsonElement actual)
    {
        Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

        foreach (JsonProperty expectedProperty in expected.EnumerateObject())
        {
            if (!actualProperties.TryGetValue(expectedProperty.Name, out JsonElement actualValue)
                || !JsonElementDeepEquals(expectedProperty.Value, actualValue))
            {
                return false;
            }
        }

        return expected.EnumerateObject().Count() == actualProperties.Count;
    }

    private static void AssertRequiredProperties(JsonElement schema, JsonElement document)
    {
        foreach (JsonElement propertyName in schema.GetProperty("required").EnumerateArray())
        {
            Assert.True(document.TryGetProperty(propertyName.GetString()!, out _), $"Missing required property '{propertyName.GetString()}'.");
        }
    }

    private static string CreateSemanticDiffProjectFixture()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "Sample.App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(directory, "GlobalUsings.cs"),
            """
            global using InfraRepo = Sample.App.Infrastructure.Repository;
            """);
        File.WriteAllText(
            Path.Combine(directory, "Handler.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    _ = new InfraRepo();
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(directory, "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
            }
            """);

        return Path.Combine(directory, "Sample.App.csproj");
    }

    private static string CreateProjectMetadataFixture()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "Sample.App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample.App.Assembly</AssemblyName>
                <RootNamespace>Sample.App</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Spectre.Console" Version="0.49.1" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(directory, "Handler.cs"),
            """
            namespace Sample.App;

            public sealed class Handler
            {
                public Repository Repository { get; } = new();
            }
            """);
        File.WriteAllText(
            Path.Combine(directory, "Repository.cs"),
            """
            namespace Sample.App;

            public sealed class Repository
            {
            }
            """);

        return Path.Combine(directory, "Sample.App.csproj");
    }
}
