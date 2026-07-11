using DotnetCoupling.Core;
using DotnetCoupling.Roslyn.Analysis;
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
    public void Render_JsonOutputWithHotspots_UsesExtendedSchemaContract()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        report = report with { Hotspots = HotspotAnalyzer.Calculate(report, count: 1) };
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.3.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.3", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement hotspot = Assert.Single(document.RootElement.GetProperty("hotspots").EnumerateArray());
        Assert.Equal(1, hotspot.GetProperty("rank").GetInt32());
        Assert.Equal("Fixture.Global.Api.Handler", hotspot.GetProperty("component").GetString());
    }

    [Fact]
    public void Render_JsonOutputWithComplexityHotspots_UsesComplexitySchemaContract()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            fixture,
            AnalysisMode.Syntax,
            volatilityProvider: null,
            gitMonths: 6,
            AnalysisOptions.Default,
            includeComplexity: true);
        report = report with { Hotspots = HotspotAnalyzer.Calculate(report, count: 1) };
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.4.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.4", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement hotspot = Assert.Single(document.RootElement.GetProperty("hotspots").EnumerateArray());
        JsonElement complexity = hotspot.GetProperty("complexity");
        Assert.Equal(1, complexity.GetProperty("maxCyclomaticComplexity").GetInt32());
        Assert.Equal(0, complexity.GetProperty("maxCognitiveComplexity").GetInt32());
        Assert.Equal("Handle", complexity.GetProperty("mostComplexMember").GetString());
        string[] runNotes = document.RootElement
            .GetProperty("manifest")
            .GetProperty("runNotes")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Contains("Sonar C# 10.27 compatible complexity profile.", runNotes);
    }

    [Fact]
    public void Render_JsonOutputWithImpact_UsesInvestigationSchemaContract()
    {
        AnalysisReport report = CreateInvestigationReport() with
        {
            Impact = new ImpactAnalysis(
                "Sample.Target",
                "Sample.Target",
                0.72,
                ["3 dependent components", "crosses a project or namespace boundary"],
                [new DependencyPath("Sample.Caller", 1, ["Sample.Target", "Sample.Caller"], "Sample.App", "Sample", true, new SourceLocation("Caller.cs", 4))],
                ["Sample.App"],
                ["Sample"],
                null),
        };
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.5.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.5", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement impact = document.RootElement.GetProperty("impact");
        Assert.Equal("Sample.Target", impact.GetProperty("component").GetString());
        Assert.Equal("Sample.Caller", impact.GetProperty("dependents")[0].GetProperty("component").GetString());
    }

    [Fact]
    public void Render_JsonOutputWithTrace_UsesInvestigationSchemaContract()
    {
        AnalysisReport report = CreateInvestigationReport() with
        {
            Trace = new TraceAnalysis(
                "Execute",
                "Sample.Target.Execute()",
                TracedSymbolKind.Member,
                [new DependencyPath("Sample.Caller", 1, ["Sample.Target.Execute()", "Sample.Caller"], "Sample.App", "Sample", false, new SourceLocation("Caller.cs", 8), "Sample.Caller.Run()")]),
        };
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("0.5", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement trace = document.RootElement.GetProperty("trace");
        Assert.Equal("Member", trace.GetProperty("kind").GetString());
        Assert.Equal("Sample.Caller.Run()", trace.GetProperty("callers")[0].GetProperty("sourceSymbol").GetString());
    }

    [Fact]
    public void Render_MarkdownWithImpact_ProducesShareableEvidence()
    {
        AnalysisReport report = CreateInvestigationReport() with
        {
            Impact = new ImpactAnalysis(
                "Sample.Target",
                "Sample.Target",
                0.72,
                ["1 dependent component", "crosses a project or namespace boundary"],
                [new DependencyPath("Sample.Caller", 1, ["Sample.Target", "Sample.Caller"], "Sample.App", "Sample", true, new SourceLocation("Caller.cs", 4))],
                ["Sample.App"],
                ["Sample"],
                null),
        };

        string rendered = ReportRenderer.Render(report, ReportFormat.Markdown);

        Assert.Contains("# dotnet-coupling report", rendered);
        Assert.Contains("## Impact: `Sample.Target`", rendered);
        Assert.Contains("Risk score: **0.72**", rendered);
        Assert.Contains("`Sample.Target` -> `Sample.Caller`", rendered);
    }

    [Fact]
    public void Render_AiOutput_ReusesIssueEvidenceAndRecommendation()
    {
        CouplingIssue issue = new(
            IssueType.GlobalComplexity,
            Severity.High,
            "Sample.Caller",
            "Sample.Target",
            0.32,
            "Caller coordinates too many dependencies.",
            "Split orchestration from policy.",
            new SourceLocation("Caller.cs", 4));
        AnalysisReport report = CreateInvestigationReport() with { Issues = [issue] };

        string rendered = ReportRenderer.Render(report, ReportFormat.Ai);

        Assert.Contains("# Coding-agent handoff", rendered);
        Assert.Contains("Caller coordinates too many dependencies.", rendered);
        Assert.Contains("Split orchestration from policy.", rendered);
        Assert.Contains("Caller.cs:4", rendered);
        Assert.Contains("Verify Grade and issue counts remain unchanged", rendered);
    }

    [Fact]
    public void Render_Markdown_IncludesRecommendationsLocationsAndAnalysisLimits()
    {
        CouplingIssue issue = new(
            IssueType.GlobalComplexity,
            Severity.High,
            "Sample.Caller",
            "Sample.Target",
            0.32,
            "Caller coordinates too many dependencies.",
            "Split orchestration from policy.",
            new SourceLocation("src/Caller.cs", 4));
        AnalysisReport report = CreateInvestigationReport() with
        {
            Issues = [issue],
            BlindSpots = ["Runtime dependency resolution is not observed."],
            Diagnostics = [new AnalysisDiagnostic("workspace-warning", "Warning", "One project could not be loaded.", "Sample.sln")],
        };

        string rendered = ReportRenderer.Render(report, ReportFormat.Markdown);

        Assert.Contains("Split orchestration from policy.", rendered);
        Assert.Contains("`src/Caller.cs:4`", rendered);
        Assert.Contains("## Diagnostics", rendered);
        Assert.Contains("workspace-warning", rendered);
        Assert.Contains("## Blind spots", rendered);
        Assert.Contains("Runtime dependency resolution is not observed.", rendered);
    }

    [Fact]
    public void Render_SummaryInJapanese_LocalizesHumanLabelsOnly()
    {
        AnalysisReport report = CreateInvestigationReport() with
        {
            Diagnostics = [new AnalysisDiagnostic("workspace-warning", "Warning", "One project could not be loaded.", "Sample.sln")],
        };

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary, ReportLanguage.Japanese);

        Assert.Contains("評価: B", rendered);
        Assert.Contains("ファイル: 2", rendered);
        Assert.Contains("問題: 0 Critical, 0 High, 0 Medium", rendered);
        Assert.Contains("Git: disabled", rendered);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", rendered);
    }

    [Fact]
    public void Render_TextAndHotspotsInJapanese_LocalizeHumanLabels()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        report = report with { Hotspots = HotspotAnalyzer.Calculate(report, 1) };

        string text = ReportRenderer.Render(report, ReportFormat.Text, ReportLanguage.Japanese);
        string hotspots = ReportRenderer.Render(report, ReportFormat.Hotspots, ReportLanguage.Japanese);

        Assert.Contains("解析対象:", text);
        Assert.Contains("上位の問題", text);
        Assert.Contains("優先順位", hotspots);
        Assert.Contains("理由:", hotspots);
    }

    [Fact]
    public void Render_HotspotsOutput_SeparatesRemediationPriorityFromHealthGrade()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fixture, useGit: false, gitMonths: 6);
        report = report with { Hotspots = HotspotAnalyzer.Calculate(report, count: 1) };

        string rendered = ReportRenderer.Render(report, ReportFormat.Hotspots);

        Assert.Contains("Priority ranking for remediation;", rendered);
        Assert.Contains("Grade remains the project health gate.", rendered);
    }

    [Fact]
    public void Render_HotspotsOutputWithComplexity_IncludesComplexitySummary()
    {
        string fixture = TestPaths.Fixture("global-complexity");
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            fixture,
            AnalysisMode.Syntax,
            volatilityProvider: null,
            gitMonths: 6,
            AnalysisOptions.Default,
            includeComplexity: true);
        report = report with { Hotspots = HotspotAnalyzer.Calculate(report, count: 1) };

        string rendered = ReportRenderer.Render(report, ReportFormat.Hotspots);

        Assert.Contains("Complexity: cyclomatic max 1, cognitive max 0, members 1", rendered);
    }

    [Fact]
    public void Render_JsonOutputWithHotspotsAndRoleContext_KeepsRoleContextInExtendedManifest()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 1,
                MatchedComponents: 1,
                UnmatchedComponents: 0,
                AccidentalVolatilityIssues: 0,
                Subdomains:
                [
                    new DomainSubdomainUsage(
                        "Contracts",
                        SubdomainCategory.Supporting,
                        Volatility.Low,
                        MatchedComponents: 1,
                        StrategicRole.PublishedLanguage),
                ],
                AreaCount: 1,
                MatchedAreaComponents: 1,
                UnmatchedAreaComponents: 0,
                Areas:
                [
                    new DomainAreaUsage(
                        "PublicApi",
                        TechnicalRole.Contract,
                        MatchedComponents: 1),
                ]),
            Hotspots:
            [
                new Hotspot(
                    Rank: 1,
                    Component: "Sample.Contracts.PublicApi",
                    Score: 0.50,
                    IssueCount: 0,
                    FanIn: 1,
                    FanOut: 1,
                    Volatility.Medium,
                    CrossesBoundary: false,
                    ParticipatesInCycle: false,
                    Reasons: ["technical role: contract"]),
            ]);

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-report-0.3.schema.json")));
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        AssertRequiredProperties(schema.RootElement, document.RootElement);
        Assert.Equal("0.3", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement domainContext = document.RootElement.GetProperty("manifest").GetProperty("domainContext");
        JsonElement subdomain = Assert.Single(domainContext.GetProperty("subdomains").EnumerateArray());
        Assert.Equal("PublishedLanguage", subdomain.GetProperty("strategicRole").GetString());
        JsonElement area = Assert.Single(domainContext.GetProperty("areas").EnumerateArray());
        Assert.Equal("Contract", area.GetProperty("technicalRole").GetString());
    }

    [Fact]
    public void Render_JsonOutputWithSuppressedIssues_UsesExtendedSchemaContract()
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
            new GradeResult("B", "Balanced", "issue-density", "Test"),
            1.0,
            [],
            [],
            [],
            [],
            [],
            SuppressedIssues: [new SuppressedIssue(issue, "Tracked in ARCH-42")]);
        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("0.3", document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement suppressedIssue = Assert.Single(document.RootElement.GetProperty("suppressedIssues").EnumerateArray());
        Assert.Equal("Tracked in ARCH-42", suppressedIssue.GetProperty("reason").GetString());
        Assert.Equal("GlobalComplexity", suppressedIssue.GetProperty("issue").GetProperty("type").GetString());
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
    public void Render_SummaryOutput_IncludesDomainContextSummary()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 1,
                MatchedComponents: 1,
                UnmatchedComponents: 1,
                AccidentalVolatilityIssues: 1,
                Subdomains:
                [
                    new DomainSubdomainUsage(
                        "Reporting",
                        SubdomainCategory.Supporting,
                        Volatility.Low,
                        MatchedComponents: 1),
                ]));

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.Contains("Domain Context: 1 subdomain configured, 1 matched component, 1 unmatched component, 1 AccidentalVolatility issue", rendered);
    }

    [Fact]
    public void Render_SummaryOutput_IncludesRoleContextSummary()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 0,
                MatchedComponents: 0,
                UnmatchedComponents: 0,
                AccidentalVolatilityIssues: 0,
                Subdomains: [],
                AreaCount: 2,
                MatchedAreaComponents: 1,
                UnmatchedAreaComponents: 1,
                Areas:
                [
                    new DomainAreaUsage(
                        "Application",
                        TechnicalRole.ApplicationService,
                        MatchedComponents: 1),
                    new DomainAreaUsage(
                        "Adapters",
                        TechnicalRole.Adapter,
                        MatchedComponents: 0),
                ]));

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.DoesNotContain("Domain Context:", rendered);
        Assert.Contains("Role Context: 2 areas configured, 1 matched component, 1 unmatched component", rendered);
    }

    [Fact]
    public void Render_SummaryOutput_IncludesDomainContextCoverageHints()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 10, 10, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 1,
                MatchedComponents: 7,
                UnmatchedComponents: 3,
                AccidentalVolatilityIssues: 0,
                Subdomains:
                [
                    new DomainSubdomainUsage(
                        "Core",
                        SubdomainCategory.Core,
                        Volatility.High,
                        MatchedComponents: 7),
                ],
                AreaCount: 1,
                MatchedAreaComponents: 6,
                UnmatchedAreaComponents: 4,
                Areas:
                [
                    new DomainAreaUsage(
                        "CoreContracts",
                        TechnicalRole.Contract,
                        MatchedComponents: 6),
                ]));

        string rendered = ReportRenderer.Render(report, ReportFormat.Summary);

        Assert.Contains("Domain Context Hint: 3 components did not match any configured subdomain.", rendered);
        Assert.Contains("Role Context Hint: 4 components did not match any configured technical role.", rendered);
    }

    [Fact]
    public void Render_JsonOutput_IncludesDomainContextManifest()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 1,
                MatchedComponents: 1,
                UnmatchedComponents: 1,
                AccidentalVolatilityIssues: 1,
                Subdomains:
                [
                    new DomainSubdomainUsage(
                        "Reporting",
                        SubdomainCategory.Supporting,
                        Volatility.Low,
                        MatchedComponents: 1),
                ]));

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement domainContext = document.RootElement.GetProperty("manifest").GetProperty("domainContext");
        Assert.Equal(1, domainContext.GetProperty("subdomainCount").GetInt32());
        Assert.Equal(1, domainContext.GetProperty("matchedComponents").GetInt32());
        Assert.Equal(1, domainContext.GetProperty("unmatchedComponents").GetInt32());
        Assert.Equal(1, domainContext.GetProperty("accidentalVolatilityIssues").GetInt32());
        JsonElement reporting = Assert.Single(domainContext.GetProperty("subdomains").EnumerateArray());
        Assert.Equal("Reporting", reporting.GetProperty("name").GetString());
        Assert.Equal("Supporting", reporting.GetProperty("category").GetString());
        Assert.Equal("Low", reporting.GetProperty("expectedVolatility").GetString());
        Assert.Equal(1, reporting.GetProperty("matchedComponents").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_IncludesRoleContextManifest()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 2, 1, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 0,
                MatchedComponents: 0,
                UnmatchedComponents: 0,
                AccidentalVolatilityIssues: 0,
                Subdomains: [],
                AreaCount: 1,
                MatchedAreaComponents: 1,
                UnmatchedAreaComponents: 0,
                Areas:
                [
                    new DomainAreaUsage(
                        "Application",
                        TechnicalRole.ApplicationService,
                        MatchedComponents: 1),
                ]));

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement domainContext = document.RootElement.GetProperty("manifest").GetProperty("domainContext");
        Assert.Equal(1, domainContext.GetProperty("areaCount").GetInt32());
        Assert.Equal(1, domainContext.GetProperty("matchedAreaComponents").GetInt32());
        Assert.Equal(0, domainContext.GetProperty("unmatchedAreaComponents").GetInt32());
        JsonElement area = Assert.Single(domainContext.GetProperty("areas").EnumerateArray());
        Assert.Equal("Application", area.GetProperty("name").GetString());
        Assert.Equal("ApplicationService", area.GetProperty("technicalRole").GetString());
        Assert.Equal(1, area.GetProperty("matchedComponents").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_IncludesDomainContextCoverageHintsInRunNotes()
    {
        AnalysisReport report = new(
            new AnalysisSummary("/tmp/sample", "syntax-only", 2, 10, 10, 0, true, true, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            [],
            DomainContext: new DomainContextSummary(
                SubdomainCount: 1,
                MatchedComponents: 7,
                UnmatchedComponents: 3,
                AccidentalVolatilityIssues: 0,
                Subdomains:
                [
                    new DomainSubdomainUsage(
                        "Core",
                        SubdomainCategory.Core,
                        Volatility.High,
                        MatchedComponents: 7),
                ],
                AreaCount: 1,
                MatchedAreaComponents: 6,
                UnmatchedAreaComponents: 4,
                Areas:
                [
                    new DomainAreaUsage(
                        "CoreContracts",
                        TechnicalRole.Contract,
                        MatchedComponents: 6),
                ]));

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        string[] runNotes = document.RootElement
            .GetProperty("manifest")
            .GetProperty("runNotes")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Contains("Domain Context coverage is partial: 3 components did not match any configured subdomain.", runNotes);
        Assert.Contains("Role Context coverage is partial: 4 components did not match any configured technical role.", runNotes);
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
        Assert.Contains("Semantic preview resolves many symbol-aware dependencies, but some flows remain syntax-equivalent.", runNotes);
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
    public void Render_JsonOutput_SyntaxAndSemanticPreview_CharacterizeSemanticOnlyDynamicDispatchDiff()
    {
        string projectPath = CreateSemanticDynamicDiffProjectFixture();

        AnalysisReport syntaxReport = CSharpDependencyAnalyzer.Analyze(projectPath, AnalysisMode.Syntax, volatilityProvider: null, gitMonths: 6);
        AnalysisReport semanticReport = CSharpDependencyAnalyzer.Analyze(projectPath, AnalysisMode.Semantic, volatilityProvider: null, gitMonths: 6);

        using JsonDocument syntaxDocument = JsonDocument.Parse(ReportRenderer.Render(syntaxReport, ReportFormat.Json));
        using JsonDocument semanticDocument = JsonDocument.Parse(ReportRenderer.Render(semanticReport, ReportFormat.Json));

        Assert.Equal("syntax-only", syntaxDocument.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal(2, syntaxDocument.RootElement.GetProperty("analysis").GetProperty("couplings").GetProperty("internal").GetInt32());
        Assert.Equal("semantic-preview", semanticDocument.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal(3, semanticDocument.RootElement.GetProperty("analysis").GetProperty("couplings").GetProperty("internal").GetInt32());
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

    [Fact]
    public void Render_JsonOutput_SlnxInputWithMissingListedProject_IncludesDiagnosticsAndLoadedProjectMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        Directory.CreateDirectory(app);
        File.WriteAllText(
            Path.Combine(root, "Sample.slnx"),
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Missing/Missing.csproj" />
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
        File.WriteAllText(
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(Path.Combine(root, "Sample.slnx"), useGit: false, gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("missing-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_SlnInputWithMissingListedProject_IncludesDiagnosticsAndLoadedProjectMetadata()
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
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "Missing\Missing.csproj", "{22222222-2222-2222-2222-222222222222}"
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
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(Path.Combine(root, "Sample.sln"), useGit: false, gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("missing-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_SemanticSlnxInputWithInvalidProject_IncludesDiagnosticsAndLoadedProjectMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
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
        File.WriteAllText(
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            Path.Combine(root, "Sample.slnx"),
            AnalysisMode.Semantic,
            volatilityProvider: null,
            gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("semantic-preview", document.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("invalid-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_SemanticSlnInputWithInvalidProject_IncludesDiagnosticsAndLoadedProjectMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string broken = Path.Combine(root, "Broken");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(broken);
        File.WriteAllText(
            Path.Combine(root, "Sample.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Broken", "Broken\Broken.csproj", "{22222222-2222-2222-2222-222222222222}"
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
        File.WriteAllText(Path.Combine(broken, "Broken.csproj"), "<Project><PropertyGroup>");
        File.WriteAllText(
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            Path.Combine(root, "Sample.sln"),
            AnalysisMode.Semantic,
            volatilityProvider: null,
            gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("semantic-preview", document.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("invalid-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_SemanticSlnxInputWithMissingListedProject_IncludesDiagnosticsAndLoadedProjectMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        Directory.CreateDirectory(app);
        File.WriteAllText(
            Path.Combine(root, "Sample.slnx"),
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Missing/Missing.csproj" />
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
        File.WriteAllText(
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            Path.Combine(root, "Sample.slnx"),
            AnalysisMode.Semantic,
            volatilityProvider: null,
            gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("semantic-preview", document.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("missing-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    [Fact]
    public void Render_JsonOutput_SemanticSlnInputWithMissingListedProject_IncludesDiagnosticsAndLoadedProjectMetadata()
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
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "Missing\Missing.csproj", "{22222222-2222-2222-2222-222222222222}"
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
            Path.Combine(app, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(
            Path.Combine(root, "Sample.sln"),
            AnalysisMode.Semantic,
            volatilityProvider: null,
            gitMonths: 6);

        using JsonDocument document = JsonDocument.Parse(ReportRenderer.Render(report, ReportFormat.Json));

        Assert.Equal("semantic-preview", document.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        JsonElement diagnostics = document.RootElement.GetProperty("manifest").GetProperty("diagnostics");
        JsonElement diagnostic = Assert.Single(diagnostics.EnumerateArray());
        Assert.Equal("missing-project", diagnostic.GetProperty("code").GetString());

        JsonElement projectModel = document.RootElement.GetProperty("projectModel");
        Assert.Equal(1, projectModel.GetProperty("projectCount").GetInt32());
        JsonElement project = Assert.Single(projectModel.GetProperty("projects").EnumerateArray());
        Assert.Equal("App", project.GetProperty("projectName").GetString());
        Assert.Equal(1, project.GetProperty("sourceFileCount").GetInt32());
    }

    private static AnalysisReport CreateInvestigationReport()
    {
        return new AnalysisReport(
            new AnalysisSummary("/tmp/sample", "semantic-preview", 2, 2, 1, 0, false, false, 6),
            new GradeResult("B", "Healthy", "issue-density", "Test"),
            0.80,
            [],
            [],
            [],
            [],
            []);
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

    private static string CreateSemanticDynamicDiffProjectFixture()
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
            Path.Combine(directory, "Handler.cs"),
            """
            using Sample.App.Infrastructure;

            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    dynamic repository = CreateRepository();
                    repository?.Save();
                }

                private static Repository CreateRepository()
                {
                    return new Repository();
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(directory, "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
                public void Save()
                {
                }
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
