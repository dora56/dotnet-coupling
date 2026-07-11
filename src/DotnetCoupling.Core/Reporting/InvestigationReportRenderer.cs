using System.Globalization;
using System.Text;

namespace DotnetCoupling.Core;

internal static class InvestigationReportRenderer
{
    public static string Render(AnalysisReport report, ReportLanguage language)
    {
        return report.Impact is not null
            ? RenderImpact(report.Impact, language)
            : report.Trace is not null
                ? RenderTrace(report.Trace, language)
                : language == ReportLanguage.Japanese
                    ? "調査結果はありません。"
                    : "No investigation result is available.";
    }

    public static string RenderImpact(ImpactAnalysis impact, ReportLanguage language)
    {
        bool japanese = language == ReportLanguage.Japanese;
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "影響分析" : "Impact analysis")}: {impact.Component}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "リスクスコア" : "Risk score")}: {impact.RiskScore:0.00}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "影響を受けるコンポーネント" : "Affected components")}: {impact.Dependents.Count}");
        foreach (DependencyPath dependent in impact.Dependents)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- depth {dependent.Depth}: {string.Join(" -> ", dependent.Path)}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string RenderTrace(TraceAnalysis trace, ReportLanguage language)
    {
        bool japanese = language == ReportLanguage.Japanese;
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "依存トレース" : "Dependency trace")}: {trace.Symbol}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{(japanese ? "呼び出し元" : "Callers")}: {trace.Callers.Count}");
        foreach (DependencyPath caller in trace.Callers)
        {
            string source = caller.SourceSymbol is null ? caller.Component : $"{caller.Component} ({caller.SourceSymbol})";
            builder.AppendLine(CultureInfo.InvariantCulture, $"- depth {caller.Depth}: {source}");
        }

        return builder.ToString().TrimEnd();
    }
}
