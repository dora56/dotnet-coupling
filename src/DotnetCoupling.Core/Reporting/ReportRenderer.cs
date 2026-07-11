using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetCoupling.Core;

public static class ReportRenderer
{
    private static readonly string ToolVersion = typeof(ReportRenderer).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        .Split('+')[0]
        ?? typeof(ReportRenderer).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Render(AnalysisReport report, ReportFormat format)
    {
        return Render(report, format, ReportLanguage.English);
    }

    public static string Render(AnalysisReport report, ReportFormat format, ReportLanguage language)
    {
        return format switch
        {
            ReportFormat.Json => RenderJson(report),
            ReportFormat.Summary when language == ReportLanguage.Japanese => RenderJapaneseSummary(report),
            ReportFormat.Summary => RenderSummary(report),
            ReportFormat.Hotspots when language == ReportLanguage.Japanese => RenderJapaneseHotspots(report),
            ReportFormat.Hotspots => RenderHotspots(report),
            ReportFormat.Markdown => MarkdownReportRenderer.Render(report, language),
            ReportFormat.Ai => AiReportRenderer.Render(report, language),
            ReportFormat.Investigation => InvestigationReportRenderer.Render(report, language),
            _ when language == ReportLanguage.Japanese => RenderJapaneseText(report),
            _ => RenderText(report),
        };
    }

    private static string RenderJapaneseText(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"解析対象: '{report.Summary.Path}'");
        builder.AppendLine(CultureInfo.InvariantCulture, $"解析完了: {report.Summary.Files} ファイル、{report.Summary.Components} 型");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"評価: {report.Grade.Letter} ({report.Grade.Display}) | 平均スコア: {report.AverageBalanceScore:0.00} | 問題: {counts.Critical} Critical, {counts.High} High, {counts.Medium} Medium");
        builder.AppendLine(CultureInfo.InvariantCulture, $"判定基準: {report.Grade.Basis}、内部結合 {report.Summary.InternalCouplings} 件");
        builder.AppendLine(DescribeGit(report));
        AppendDomainContextSummary(builder, report);
        AppendSuppressedSummary(builder, report);
        AppendBaselineText(builder, report);
        builder.AppendLine();
        builder.AppendLine("上位の問題");
        builder.AppendLine("------------------------------------------------------------");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("問題は検出されませんでした。");
        }
        else
        {
            foreach ((CouplingIssue issue, int index) in report.Issues.Take(10).Select((issue, index) => (issue, index + 1)))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"{index}. {issue.Source} -> {issue.Target}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   種類: {issue.Type} | 重要度: {issue.Severity} | スコア: {issue.Score:0.00}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   問題: {issue.Problem}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   推奨: {issue.Recommendation}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderJapaneseSummary(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"評価: {report.Grade.Letter} | 平均スコア: {report.AverageBalanceScore:0.00} | 判定基準: {report.Grade.Basis}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ファイル: {report.Summary.Files} | 型: {report.Summary.Components} | 結合: 内部 {report.Summary.InternalCouplings} / 外部 {report.Summary.ExternalCouplings}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"問題: {counts.Critical} Critical, {counts.High} High, {counts.Medium} Medium");
        if (!string.Equals(report.Summary.Mode, "syntax-only", StringComparison.Ordinal))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"解析モード: {report.Summary.Mode}");
        }

        builder.AppendLine(DescribeGit(report));
        AppendDomainContextSummary(builder, report);
        AppendDiagnosticsSummary(builder, report);
        AppendSuppressedSummary(builder, report);
        AppendBaselineSummary(builder, report);
        if (report.Grade.Letter == "S")
        {
            builder.AppendLine("評価: S (過剰最適化の警告)");
            builder.AppendLine("これは表彰ではありません。抽象化過多または閾値が厳しすぎる可能性があります。");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderText(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Analyzing project at '{report.Summary.Path}'...");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Analysis complete: {report.Summary.Files} files, {report.Summary.Components} types");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Grade: {report.Grade.Letter} ({report.Grade.Display}) | Avg Score: {report.AverageBalanceScore:0.00} | Issues: {counts.Critical} Critical, {counts.High} High, {counts.Medium} Medium");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Grade basis: {report.Grade.Basis} across {report.Summary.InternalCouplings} internal couplings");
        builder.AppendLine(DescribeGit(report));
        AppendDomainContextSummary(builder, report);
        AppendSuppressedSummary(builder, report);
        builder.AppendLine("Analysis confidence: syntax-only");
        AppendBaselineText(builder, report);
        builder.AppendLine();
        builder.AppendLine("Top Issues");
        builder.AppendLine("------------------------------------------------------------");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("No issues detected.");
        }
        else
        {
            foreach ((CouplingIssue issue, int index) in report.Issues.Take(10).Select((issue, index) => (issue, index + 1)))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"{index}. {issue.Source} -> {issue.Target}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Type: {issue.Type}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Severity: {issue.Severity}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Score: {issue.Score:0.00}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Problem: {issue.Problem}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Fix: {issue.Recommendation}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderSummary(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Grade: {report.Grade.Letter} | Avg Score: {report.AverageBalanceScore:0.00} | Basis: {report.Grade.Basis}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Files: {report.Summary.Files} | Types: {report.Summary.Components} | Couplings: {report.Summary.InternalCouplings} internal / {report.Summary.ExternalCouplings} external");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Issues: {counts.Critical} Critical, {counts.High} High, {counts.Medium} Medium");
        if (!string.Equals(report.Summary.Mode, "syntax-only", StringComparison.Ordinal))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Mode: {report.Summary.Mode}");
        }
        builder.AppendLine(DescribeGit(report));
        AppendDomainContextSummary(builder, report);
        AppendDiagnosticsSummary(builder, report);
        AppendSuppressedSummary(builder, report);
        AppendBaselineSummary(builder, report);
        if (report.Grade.Letter == "S")
        {
            builder.AppendLine("Grade: S (Over-optimized warning)");
            builder.AppendLine("This is not a trophy. It may mean the project is over-abstracted or the thresholds are too strict.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderHotspots(AnalysisReport report)
    {
        IReadOnlyList<Hotspot> hotspots = report.Hotspots ?? [];
        StringBuilder builder = new();
        builder.AppendLine("Hotspots");
        builder.AppendLine("Priority ranking for remediation; Grade remains the project health gate.");
        builder.AppendLine("------------------------------------------------------------");
        if (hotspots.Count == 0)
        {
            builder.AppendLine("No hotspots detected.");
            return builder.ToString().TrimEnd();
        }

        foreach (Hotspot hotspot in hotspots)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{hotspot.Rank}. {hotspot.Component}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"   Priority: {hotspot.Score:0.00}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"   Issues: {hotspot.IssueCount} | Fan-in: {hotspot.FanIn} | Fan-out: {hotspot.FanOut} | Volatility: {hotspot.Volatility}");
            if (hotspot.Complexity is not null)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"   Complexity: cyclomatic max {hotspot.Complexity.MaxCyclomaticComplexity}, cognitive max {hotspot.Complexity.MaxCognitiveComplexity}, members {hotspot.Complexity.MemberCount}");
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"   Reasons: {string.Join(", ", hotspot.Reasons)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderJapaneseHotspots(AnalysisReport report)
    {
        IReadOnlyList<Hotspot> hotspots = report.Hotspots ?? [];
        StringBuilder builder = new();
        builder.AppendLine("ホットスポット");
        builder.AppendLine("修正の優先順位です。Grade はプロジェクト健全性の判定として維持されます。");
        builder.AppendLine("------------------------------------------------------------");
        if (hotspots.Count == 0)
        {
            builder.AppendLine("ホットスポットは検出されませんでした。");
            return builder.ToString().TrimEnd();
        }

        foreach (Hotspot hotspot in hotspots)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{hotspot.Rank}. {hotspot.Component}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"   優先度: {hotspot.Score:0.00}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"   問題: {hotspot.IssueCount} | Fan-in: {hotspot.FanIn} | Fan-out: {hotspot.FanOut} | 揮発性: {hotspot.Volatility}");
            if (hotspot.Complexity is not null)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"   複雑度: cyclomatic 最大 {hotspot.Complexity.MaxCyclomaticComplexity}, cognitive 最大 {hotspot.Complexity.MaxCognitiveComplexity}, members {hotspot.Complexity.MemberCount}");
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"   理由: {string.Join(", ", hotspot.Reasons)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderJson(AnalysisReport report)
    {
        object document = HasExtendedJson(report)
            ? CreateExtendedJsonDocument(report)
            : report.Baseline is null ? CreateJsonDocument(report) : CreateJsonDocumentWithBaseline(report);

        string json = JsonSerializer.Serialize(document, JsonOptions);
        return json.Replace("\"schema\"", "\"$schema\"", StringComparison.Ordinal);
    }

    private static bool HasExtendedJson(AnalysisReport report)
    {
        return report.Hotspots is not null
            || report.SuppressedIssues is { Count: > 0 }
            || report.Impact is not null
            || report.Trace is not null;
    }

    private static Dictionary<string, object?> CreateJsonDocument(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        Dictionary<string, object?> document = new(StringComparer.Ordinal)
        {
            ["Schema"] = "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report.schema.json",
            ["SchemaVersion"] = "0.1",
            ["Tool"] = "dotnet-coupling",
            ["Version"] = ToolVersion,
            ["Analysis"] = CreateAnalysisJson(report),
            ["Grade"] = report.Grade,
            ["Scores"] = new
            {
                AverageBalanceScore = report.AverageBalanceScore,
            },
            ["IssueCounts"] = new
            {
                counts.Critical,
                counts.High,
                counts.Medium,
                counts.Low,
            },
            ["Issues"] = report.Issues,
            ["Manifest"] = CreateManifestJson(report),
        };

        if (report.ProjectMetadata is not null)
        {
            document["ProjectModel"] = CreateProjectModelJson(report.ProjectMetadata);
        }

        return document;
    }

    private static Dictionary<string, object?> CreateExtendedJsonDocument(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        bool hasInvestigation = report.Impact is not null || report.Trace is not null;
        bool hasComplexityHotspots = HasComplexityHotspots(report);
        Dictionary<string, object?> document = new(StringComparer.Ordinal)
        {
            ["Schema"] = hasInvestigation
                ? "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report-0.5.schema.json"
                : hasComplexityHotspots
                    ? "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report-0.4.schema.json"
                    : "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report-0.3.schema.json",
            ["SchemaVersion"] = hasInvestigation ? "0.5" : hasComplexityHotspots ? "0.4" : "0.3",
            ["Tool"] = "dotnet-coupling",
            ["Version"] = ToolVersion,
            ["Analysis"] = CreateAnalysisJson(report),
            ["Grade"] = report.Grade,
            ["Scores"] = new
            {
                AverageBalanceScore = report.AverageBalanceScore,
            },
            ["IssueCounts"] = new
            {
                counts.Critical,
                counts.High,
                counts.Medium,
                counts.Low,
            },
            ["Issues"] = report.Issues,
            ["Manifest"] = CreateManifestJson(report),
        };

        if (report.Baseline is not null)
        {
            BaselineComparison baseline = report.Baseline;
            document["Baseline"] = new
            {
                Ref = baseline.Ref,
                New = CountIssues(baseline.NewIssues),
                Resolved = CountIssues(baseline.ResolvedIssues),
                Unchanged = CountIssues(baseline.UnchangedIssues),
                NewIssues = baseline.NewIssues,
                ResolvedIssues = baseline.ResolvedIssues,
                UnchangedIssues = baseline.UnchangedIssues,
            };
        }

        if (report.ProjectMetadata is not null)
        {
            document["ProjectModel"] = CreateProjectModelJson(report.ProjectMetadata);
        }

        if (report.Hotspots is not null)
        {
            document["Hotspots"] = CreateHotspotsJson(report.Hotspots);
        }

        if (report.SuppressedIssues is { Count: > 0 })
        {
            document["SuppressedIssues"] = report.SuppressedIssues;
        }

        if (report.Impact is not null)
        {
            document["Impact"] = report.Impact;
        }

        if (report.Trace is not null)
        {
            document["Trace"] = report.Trace;
        }

        return document;
    }

    private static bool HasComplexityHotspots(AnalysisReport report)
    {
        return report.Hotspots?.Any(hotspot => hotspot.Complexity is not null) == true;
    }

    private static Dictionary<string, object?>[] CreateHotspotsJson(IReadOnlyList<Hotspot> hotspots)
    {
        return hotspots.Select(hotspot =>
        {
            Dictionary<string, object?> json = new(StringComparer.Ordinal)
            {
                ["Rank"] = hotspot.Rank,
                ["Component"] = hotspot.Component,
                ["Score"] = hotspot.Score,
                ["IssueCount"] = hotspot.IssueCount,
                ["FanIn"] = hotspot.FanIn,
                ["FanOut"] = hotspot.FanOut,
                ["Volatility"] = hotspot.Volatility,
                ["CrossesBoundary"] = hotspot.CrossesBoundary,
                ["ParticipatesInCycle"] = hotspot.ParticipatesInCycle,
                ["Reasons"] = hotspot.Reasons,
            };

            if (hotspot.Complexity is not null)
            {
                json["Complexity"] = hotspot.Complexity;
            }

            return json;
        }).ToArray();
    }

    private static Dictionary<string, object?> CreateJsonDocumentWithBaseline(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        BaselineComparison baseline = report.Baseline ?? throw new InvalidOperationException("Baseline comparison is required.");
        Dictionary<string, object?> document = new(StringComparer.Ordinal)
        {
            ["Schema"] = "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report-0.2.schema.json",
            ["SchemaVersion"] = "0.2",
            ["Tool"] = "dotnet-coupling",
            ["Version"] = ToolVersion,
            ["Analysis"] = CreateAnalysisJson(report),
            ["Grade"] = report.Grade,
            ["Scores"] = new
            {
                AverageBalanceScore = report.AverageBalanceScore,
            },
            ["IssueCounts"] = new
            {
                counts.Critical,
                counts.High,
                counts.Medium,
                counts.Low,
            },
            ["Issues"] = report.Issues,
            ["Baseline"] = new
            {
                Ref = baseline.Ref,
                New = CountIssues(baseline.NewIssues),
                Resolved = CountIssues(baseline.ResolvedIssues),
                Unchanged = CountIssues(baseline.UnchangedIssues),
                NewIssues = baseline.NewIssues,
                ResolvedIssues = baseline.ResolvedIssues,
                UnchangedIssues = baseline.UnchangedIssues,
            },
            ["Manifest"] = CreateManifestJson(report),
        };

        if (report.ProjectMetadata is not null)
        {
            document["ProjectModel"] = CreateProjectModelJson(report.ProjectMetadata);
        }

        return document;
    }

    private static object CreateAnalysisJson(AnalysisReport report)
    {
        return new
        {
            report.Summary.Path,
            report.Summary.Mode,
            report.Summary.Files,
            Components = report.Summary.Components,
            Couplings = new
            {
                Total = report.Summary.InternalCouplings + report.Summary.ExternalCouplings,
                Internal = report.Summary.InternalCouplings,
                External = report.Summary.ExternalCouplings,
            },
            report.Summary.GitUsed,
            report.Summary.GitMonths,
        };
    }

    private static string DescribeGit(AnalysisReport report)
    {
        if (!report.Summary.GitRequested)
        {
            return "Git: disabled (--no-git)";
        }

        return report.Summary.GitUsed
            ? $"Git: used ({report.Summary.GitMonths} months)"
            : "Git: unavailable or no matching history";
    }

    private static Dictionary<string, object?> CreateManifestJson(AnalysisReport report)
    {
        Dictionary<string, object?> manifest = new(StringComparer.Ordinal)
        {
            ["confidence"] = report.Summary.Mode,
            ["runNotes"] = CreateRunNotes(report),
            ["blindSpots"] = report.BlindSpots.Select(blindSpot =>
            {
                string kind = blindSpot.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Unknown";
                return new
                {
                    Kind = kind,
                    Description = blindSpot,
                };
            }),
        };

        if (report.Diagnostics is { Count: > 0 })
        {
            manifest["diagnostics"] = report.Diagnostics;
        }

        if (report.DomainContext is not null)
        {
            manifest["domainContext"] = report.DomainContext;
        }

        return manifest;
    }

    private static object CreateProjectModelJson(ProjectMetadata projectMetadata)
    {
        return new
        {
            projectMetadata.ProjectCount,
            Projects = projectMetadata.Projects.Select(project => new
            {
                project.ProjectPath,
                project.ProjectName,
                project.AssemblyName,
                project.SourceFileCount,
                project.ProjectReferences,
                project.PackageReferences,
            }),
        };
    }

    private static List<string> CreateRunNotes(AnalysisReport report)
    {
        List<string> notes;
        if (string.Equals(report.Summary.Mode, "semantic-preview", StringComparison.Ordinal))
        {
            notes =
            [
                "Semantic mode uses MSBuildWorkspace preview loading.",
                "Semantic preview resolves many symbol-aware dependencies, but some flows remain syntax-equivalent.",
            ];
        }
        else
        {
            notes = ["Semantic symbol resolution is not enabled."];
        }

        if (report.ComponentComplexities is not null)
        {
            notes.Add("Sonar C# 10.27 compatible complexity profile.");
        }

        notes.AddRange(CreateDomainContextCoverageNotes(report));
        return notes;
    }

    private static void AppendDiagnosticsSummary(StringBuilder builder, AnalysisReport report)
    {
        if (report.Diagnostics is not { Count: > 0 })
        {
            return;
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Diagnostics: {report.Diagnostics.Count} recoverable warning(s)");
    }

    private static void AppendSuppressedSummary(StringBuilder builder, AnalysisReport report)
    {
        if (report.SuppressedIssues is not { Count: > 0 })
        {
            return;
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Suppressed Issues: {report.SuppressedIssues.Count}");
    }

    private static void AppendDomainContextSummary(StringBuilder builder, AnalysisReport report)
    {
        if (report.DomainContext is null)
        {
            return;
        }

        DomainContextSummary domainContext = report.DomainContext;
        if (domainContext.SubdomainCount > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Domain Context: {domainContext.SubdomainCount} {Pluralize(domainContext.SubdomainCount, "subdomain")} configured, {domainContext.MatchedComponents} {Pluralize(domainContext.MatchedComponents, "matched component")}, {domainContext.UnmatchedComponents} {Pluralize(domainContext.UnmatchedComponents, "unmatched component")}, {domainContext.AccidentalVolatilityIssues} {Pluralize(domainContext.AccidentalVolatilityIssues, "AccidentalVolatility issue")}");
            if (domainContext.UnmatchedComponents > 0)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"Domain Context Hint: {domainContext.UnmatchedComponents} {Pluralize(domainContext.UnmatchedComponents, "component")} did not match any configured subdomain.");
            }
        }

        if (domainContext.AreaCount > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Role Context: {domainContext.AreaCount} {Pluralize(domainContext.AreaCount, "area")} configured, {domainContext.MatchedAreaComponents} {Pluralize(domainContext.MatchedAreaComponents, "matched component")}, {domainContext.UnmatchedAreaComponents} {Pluralize(domainContext.UnmatchedAreaComponents, "unmatched component")}");
            if (domainContext.UnmatchedAreaComponents > 0)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"Role Context Hint: {domainContext.UnmatchedAreaComponents} {Pluralize(domainContext.UnmatchedAreaComponents, "component")} did not match any configured technical role.");
            }
        }
    }

    private static List<string> CreateDomainContextCoverageNotes(AnalysisReport report)
    {
        if (report.DomainContext is not DomainContextSummary domainContext)
        {
            return [];
        }

        List<string> notes = [];
        if (domainContext.SubdomainCount > 0 && domainContext.UnmatchedComponents > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"Domain Context coverage is partial: {domainContext.UnmatchedComponents} {Pluralize(domainContext.UnmatchedComponents, "component")} did not match any configured subdomain."));
        }

        if (domainContext.AreaCount > 0 && domainContext.UnmatchedAreaComponents > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"Role Context coverage is partial: {domainContext.UnmatchedAreaComponents} {Pluralize(domainContext.UnmatchedAreaComponents, "component")} did not match any configured technical role."));
        }

        return notes;
    }

    private static string Pluralize(int count, string singular)
    {
        return count == 1 ? singular : singular + "s";
    }

    private static void AppendBaselineText(StringBuilder builder, AnalysisReport report)
    {
        if (report.Baseline is null)
        {
            return;
        }

        builder.AppendLine();
        AppendBaselineSummary(builder, report);
    }

    private static void AppendBaselineSummary(StringBuilder builder, AnalysisReport report)
    {
        if (report.Baseline is null)
        {
            return;
        }

        IssueCounts newCounts = CountIssues(report.Baseline.NewIssues);
        IssueCounts resolvedCounts = CountIssues(report.Baseline.ResolvedIssues);
        builder.AppendLine(CultureInfo.InvariantCulture, $"Baseline: {report.Baseline.Ref}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"New Issues: {newCounts.Critical} Critical, {newCounts.High} High, {newCounts.Medium} Medium, {newCounts.Low} Low");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Resolved Issues: {resolvedCounts.Critical} Critical, {resolvedCounts.High} High, {resolvedCounts.Medium} Medium, {resolvedCounts.Low} Low");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Unchanged Issues: {report.Baseline.UnchangedIssues.Count}");
    }

    private static IssueCounts CountIssues(AnalysisReport report)
    {
        return new IssueCounts(
            report.Issues.Count(issue => issue.Severity == Severity.Critical),
            report.Issues.Count(issue => issue.Severity == Severity.High),
            report.Issues.Count(issue => issue.Severity == Severity.Medium),
            report.Issues.Count(issue => issue.Severity == Severity.Low));
    }

    private static IssueCounts CountIssues(IReadOnlyList<CouplingIssue> issues)
    {
        return new IssueCounts(
            issues.Count(issue => issue.Severity == Severity.Critical),
            issues.Count(issue => issue.Severity == Severity.High),
            issues.Count(issue => issue.Severity == Severity.Medium),
            issues.Count(issue => issue.Severity == Severity.Low));
    }

    private sealed record IssueCounts(int Critical, int High, int Medium, int Low);
}
