using System.Globalization;
using System.Text;

namespace DotnetCoupling.Core;

internal static class AiReportRenderer
{
    public static string Render(AnalysisReport report, ReportLanguage language)
    {
        bool japanese = language == ReportLanguage.Japanese;
        StringBuilder builder = new();
        builder.AppendLine(japanese ? "# コーディングエージェント向け引き継ぎ" : "# Coding-agent handoff");
        builder.AppendLine();
        builder.AppendLine(japanese ? "## 観測された根拠" : "## Observed evidence");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Grade: {report.Grade.Letter}; average Balance Score: {report.AverageBalanceScore:0.00}; mode: {report.Summary.Mode}");
        if (report.Impact is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- Impact target: {report.Impact.Component}; risk: {report.Impact.RiskScore:0.00}; dependents: {report.Impact.Dependents.Count}");
        }

        if (report.Trace is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- Trace target: {report.Trace.Symbol}; callers: {report.Trace.Callers.Count}");
        }

        foreach (CouplingIssue issue in report.Issues.Take(10))
        {
            string location = issue.Location is null ? "" : $" ({issue.Location.File}:{issue.Location.Line})";
            builder.AppendLine(CultureInfo.InvariantCulture, $"- [{issue.Severity}] {issue.Source} -> {issue.Target}{location}: {issue.Problem}");
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 優先候補" : "## Priority candidates");
        if (report.Hotspots is not { Count: > 0 })
        {
            builder.AppendLine(japanese ? "- 優先候補はありません。" : "- No priority candidates were identified.");
        }
        else
        {
            foreach (Hotspot hotspot in report.Hotspots.Take(10))
            {
                builder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"- {hotspot.Rank}. {hotspot.Component} ({hotspot.Score:0.00}): {string.Join(", ", hotspot.Reasons)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 範囲を限定した作業候補" : "## Bounded suggested work");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine(japanese ? "- 検出結果から推奨できる変更はありません。" : "- No change is recommended from the available findings.");
        }
        else
        {
            foreach (string recommendation in report.Issues.Select(issue => issue.Recommendation).Distinct(StringComparer.Ordinal).Take(10))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {recommendation}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(japanese ? "## 検証条件" : "## Verification constraints");
        builder.AppendLine(japanese
            ? "- 変更前後で Grade と問題件数が意図せず変わらないことを確認する。"
            : "- Verify Grade and issue counts remain unchanged unless the findings are intentionally resolved.");
        builder.AppendLine(japanese
            ? "- 推測ではなく、記録されたパスと位置情報を確認してから編集する。"
            : "- Inspect recorded paths and locations before editing; do not infer unobserved behavior.");
        return builder.ToString().TrimEnd();
    }
}
