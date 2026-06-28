using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetCoupling.Core;

public static class ReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Render(AnalysisReport report, ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Json => RenderJson(report),
            ReportFormat.Summary => RenderSummary(report),
            _ => RenderText(report),
        };
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
        AppendDiagnosticsSummary(builder, report);
        AppendBaselineSummary(builder, report);
        if (report.Grade.Letter == "S")
        {
            builder.AppendLine("Grade: S (Over-optimized warning)");
            builder.AppendLine("This is not a trophy. It may mean the project is over-abstracted or the thresholds are too strict.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderJson(AnalysisReport report)
    {
        object document = report.Baseline is null ? CreateJsonDocument(report) : CreateJsonDocumentWithBaseline(report);

        string json = JsonSerializer.Serialize(document, JsonOptions);
        return json.Replace("\"schema\"", "\"$schema\"", StringComparison.Ordinal);
    }

    private static object CreateJsonDocument(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        return new
        {
            Schema = "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report.schema.json",
            SchemaVersion = "0.1",
            Tool = "dotnet-coupling",
            Version = "0.2.0-alpha.1",
            Analysis = CreateAnalysisJson(report),
            Grade = report.Grade,
            Scores = new
            {
                AverageBalanceScore = report.AverageBalanceScore,
            },
            IssueCounts = new
            {
                counts.Critical,
                counts.High,
                counts.Medium,
                counts.Low,
            },
            Issues = report.Issues,
            Manifest = CreateManifestJson(report),
        };
    }

    private static object CreateJsonDocumentWithBaseline(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        BaselineComparison baseline = report.Baseline ?? throw new InvalidOperationException("Baseline comparison is required.");
        return new
        {
            Schema = "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report-0.2.schema.json",
            SchemaVersion = "0.2",
            Tool = "dotnet-coupling",
            Version = "0.2.0-alpha.1",
            Analysis = CreateAnalysisJson(report),
            Grade = report.Grade,
            Scores = new
            {
                AverageBalanceScore = report.AverageBalanceScore,
            },
            IssueCounts = new
            {
                counts.Critical,
                counts.High,
                counts.Medium,
                counts.Low,
            },
            Issues = report.Issues,
            Baseline = new
            {
                Ref = baseline.Ref,
                New = CountIssues(baseline.NewIssues),
                Resolved = CountIssues(baseline.ResolvedIssues),
                Unchanged = CountIssues(baseline.UnchangedIssues),
                NewIssues = baseline.NewIssues,
                ResolvedIssues = baseline.ResolvedIssues,
                UnchangedIssues = baseline.UnchangedIssues,
            },
            Manifest = CreateManifestJson(report),
        };
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

        return manifest;
    }

    private static IReadOnlyList<string> CreateRunNotes(AnalysisReport report)
    {
        if (string.Equals(report.Summary.Mode, "semantic-preview", StringComparison.Ordinal))
        {
            return
            [
                "Semantic mode uses MSBuildWorkspace preview loading.",
                "Some symbol resolution features are still syntax-equivalent.",
            ];
        }

        return ["Semantic symbol resolution is not enabled."];
    }

    private static void AppendDiagnosticsSummary(StringBuilder builder, AnalysisReport report)
    {
        if (report.Diagnostics is not { Count: > 0 })
        {
            return;
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Diagnostics: {report.Diagnostics.Count} recoverable warning(s)");
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
