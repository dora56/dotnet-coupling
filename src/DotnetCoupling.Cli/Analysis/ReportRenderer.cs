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
        StringBuilder builder = new();
        builder.AppendLine($"Analyzing project at '{report.Summary.Path}'...");
        builder.AppendLine($"Analysis complete: {report.Summary.Files} files, {report.Summary.Components} types");
        builder.AppendLine();
        builder.AppendLine($"Grade: {report.Grade.Letter} ({report.Grade.Display}) | Avg Score: {report.AverageBalanceScore:0.00} | Issues: 0 Critical, 0 High, 0 Medium");
        builder.AppendLine($"Grade basis: {report.Grade.Basis} across {report.Summary.InternalCouplings} internal couplings");
        builder.AppendLine("Analysis confidence: syntax-only");
        builder.AppendLine();
        builder.AppendLine("Top Issues");
        builder.AppendLine("------------------------------------------------------------");
        builder.AppendLine("No issues detected in Phase 0 bootstrap mode.");
        return builder.ToString().TrimEnd();
    }

    private static string RenderSummary(AnalysisReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Grade: {report.Grade.Letter} | Avg Score: {report.AverageBalanceScore:0.00} | Basis: {report.Grade.Basis}");
        builder.AppendLine($"Files: {report.Summary.Files} | Types: {report.Summary.Components} | Couplings: {report.Summary.InternalCouplings} internal / {report.Summary.ExternalCouplings} external");
        builder.AppendLine("Issues: 0 Critical, 0 High, 0 Medium");
        return builder.ToString().TrimEnd();
    }

    private static string RenderJson(AnalysisReport report)
    {
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
                Critical = 0,
                High = 0,
                Medium = 0,
                Low = 0,
            },
            Issues = report.Issues,
            Manifest = new
            {
                Confidence = "syntax-only",
                RunNotes = new[] { "Semantic symbol resolution is not enabled." },
                BlindSpots = report.BlindSpots.Select(blindSpot => new
                {
                    Kind = blindSpot.Split(' ', 2)[0],
                    Description = blindSpot,
                }),
            },
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        return json.Replace("\"schema\"", "\"$schema\"", StringComparison.Ordinal);
    }
}
