using System.Globalization;
using System.Text;

namespace DotnetCoupling.Core;

internal static class MarkdownReportRenderer
{
    public static string Render(AnalysisReport report, ReportLanguage language)
    {
        bool japanese = language == ReportLanguage.Japanese;
        StringBuilder builder = new();
        builder.AppendLine(japanese ? "# dotnet-coupling レポート" : "# dotnet-coupling report");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- **{(japanese ? "評価" : "Grade")}:** {report.Grade.Letter} ({report.Grade.Display})");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- **{(japanese ? "平均スコア" : "Average score")}:** {report.AverageBalanceScore:0.00}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- **{(japanese ? "解析モード" : "Mode")}:** {report.Summary.Mode}");

        if (report.Impact is not null)
        {
            AppendImpact(builder, report.Impact, japanese);
        }

        if (report.Trace is not null)
        {
            AppendTrace(builder, report.Trace, japanese);
        }

        AppendHotspots(builder, report.Hotspots ?? [], japanese);

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 検出された問題" : "## Issues");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine(japanese ? "問題は検出されませんでした。" : "No issues detected.");
        }
        else
        {
            foreach (CouplingIssue issue in report.Issues.Take(20))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- **{issue.Severity} / {issue.Type}** `{Escape(issue.Source)}` -> `{Escape(issue.Target)}`: {Escape(issue.Problem)}");
                if (issue.Location is not null)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  - {(japanese ? "場所" : "Location")}: `{Escape(issue.Location.File)}:{issue.Location.Line}`");
                }

                builder.AppendLine(CultureInfo.InvariantCulture, $"  - {(japanese ? "推奨" : "Recommendation")}: {Escape(issue.Recommendation)}");
            }
        }

        AppendDiagnostics(builder, report.Diagnostics ?? [], japanese);
        AppendBlindSpots(builder, report.BlindSpots, japanese);

        return builder.ToString().TrimEnd();
    }

    private static void AppendImpact(StringBuilder builder, ImpactAnalysis impact, bool japanese)
    {
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"## {(japanese ? "影響" : "Impact")}: `{Escape(impact.Component)}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "リスクスコア" : "Risk score")}: **{impact.RiskScore:0.00}**");
        foreach (string reason in impact.Reasons)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- {Escape(reason)}");
        }

        foreach (DependencyPath dependent in impact.Dependents)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- `{string.Join("` -> `", dependent.Path.Select(Escape))}` (depth {dependent.Depth})");
        }
    }

    private static void AppendTrace(StringBuilder builder, TraceAnalysis trace, bool japanese)
    {
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"## {(japanese ? "トレース" : "Trace")}: `{Escape(trace.Symbol)}`");
        foreach (DependencyPath caller in trace.Callers)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- `{string.Join("` -> `", caller.Path.Select(Escape))}` (depth {caller.Depth})");
        }
    }

    private static void AppendHotspots(
        StringBuilder builder,
        IReadOnlyList<Hotspot> hotspots,
        bool japanese)
    {
        if (hotspots.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 優先候補" : "## Priority candidates");
        foreach (Hotspot hotspot in hotspots)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"- **{hotspot.Rank}. `{Escape(hotspot.Component)}` ({hotspot.Score:0.00})**: {string.Join(", ", hotspot.Reasons.Select(Escape))}");
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static void AppendDiagnostics(
        StringBuilder builder,
        IReadOnlyList<AnalysisDiagnostic> diagnostics,
        bool japanese)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 診断" : "## Diagnostics");
        foreach (AnalysisDiagnostic diagnostic in diagnostics)
        {
            string path = diagnostic.Path is null ? "" : $" (`{Escape(diagnostic.Path)}`)";
            builder.AppendLine(CultureInfo.InvariantCulture, $"- **{diagnostic.Severity} / {diagnostic.Code}**{path}: {Escape(diagnostic.Message)}");
        }
    }

    private static void AppendBlindSpots(
        StringBuilder builder,
        IReadOnlyList<string> blindSpots,
        bool japanese)
    {
        if (blindSpots.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 解析できない領域" : "## Blind spots");
        foreach (string blindSpot in blindSpots)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- {Escape(blindSpot)}");
        }
    }
}
