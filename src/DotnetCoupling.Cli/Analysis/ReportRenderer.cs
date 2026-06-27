using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DotnetCoupling.Cli.Analysis;

public static class ReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
        builder.AppendLine("Analysis confidence: syntax-only");
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
        if (report.Grade.Letter == "S")
        {
            builder.AppendLine("Grade: S (Over-optimized warning)");
            builder.AppendLine("This is not a trophy. It may mean the project is over-abstracted or the thresholds are too strict.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderJson(AnalysisReport report)
    {
        IssueCounts counts = CountIssues(report);
        object document = new
        {
            Schema = "https://raw.githubusercontent.com/dora56/dotnet-coupling/main/schemas/dotnet-coupling-report.schema.json",
            SchemaVersion = "0.1",
            Tool = "dotnet-coupling",
            Version = "0.1.0-alpha.1",
            Analysis = new
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
            },
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
            Manifest = new
            {
                Confidence = "syntax-only",
                RunNotes = new[] { "Semantic symbol resolution is not enabled." },
                BlindSpots = report.BlindSpots.Select(blindSpot =>
                {
                    string kind = blindSpot.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Unknown";
                    return new
                    {
                        Kind = kind,
                        Description = blindSpot,
                    };
                }),
            },
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        return json.Replace("\"schema\"", "\"$schema\"", StringComparison.Ordinal);
    }

    private static IssueCounts CountIssues(AnalysisReport report)
    {
        return new IssueCounts(
            report.Issues.Count(issue => issue.Severity == Severity.Critical),
            report.Issues.Count(issue => issue.Severity == Severity.High),
            report.Issues.Count(issue => issue.Severity == Severity.Medium),
            report.Issues.Count(issue => issue.Severity == Severity.Low));
    }

    private sealed record IssueCounts(int Critical, int High, int Medium, int Low);
}
