using DotnetCoupling.Core;
using DotnetCoupling.Sarif;

namespace DotnetCoupling.Cli;

internal sealed record CliReportRenderOptions(
    bool Summary,
    bool Json,
    bool Sarif,
    bool IncludeHotspots,
    bool Markdown,
    bool Ai,
    bool Investigation,
    ReportLanguage Language,
    bool Check,
    string AnalysisTargetPath);

internal static class CliReportWriter
{
    public static bool ShouldIncludeHotspots(bool hotspotsRequested, bool jsonRequested, bool sarifRequested)
    {
        return hotspotsRequested && (jsonRequested || !sarifRequested);
    }

    public static AnalysisReport AddHotspotsIfIncluded(
        AnalysisReport report,
        bool includeHotspots,
        int? hotspotsValue,
        PrioritizationOptions prioritization)
    {
        if (!includeHotspots)
        {
            return report;
        }

        IReadOnlyList<Hotspot> hotspots = hotspotsValue is int hotspotCount
            ? HotspotAnalyzer.Calculate(report, hotspotCount, prioritization)
            : HotspotAnalyzer.Calculate(report, HotspotAnalyzer.DefaultCount, prioritization);
        return report with { Hotspots = hotspots };
    }

    public static string Render(AnalysisReport report, CliReportRenderOptions options)
    {
        if (options.Json)
        {
            return ReportRenderer.Render(report, ReportFormat.Json);
        }

        if (options.Sarif)
        {
            string repositoryRoot = CliPathResolver.FindGitRepositoryRoot(options.AnalysisTargetPath)
                ?? CliPathResolver.ResolveOutputRoot(options.AnalysisTargetPath);
            return SarifReportRenderer.Render(report, repositoryRoot);
        }

        if (options.Ai)
        {
            return ReportRenderer.Render(report, ReportFormat.Ai, options.Language);
        }

        if (options.Markdown)
        {
            return ReportRenderer.Render(report, ReportFormat.Markdown, options.Language);
        }

        if (options.Investigation)
        {
            return ReportRenderer.Render(report, ReportFormat.Investigation, options.Language);
        }

        ReportFormat format = options.IncludeHotspots
            ? ReportFormat.Hotspots
            : options.Summary || options.Check
                ? ReportFormat.Summary
                : ReportFormat.Text;
        return ReportRenderer.Render(report, format, options.Language);
    }

    public static void Write(string rendered, FileInfo? output)
    {
        if (output is null)
        {
            Console.WriteLine(rendered);
            return;
        }

        DirectoryInfo? outputDirectory = output.Directory;
        if (outputDirectory is not null && !outputDirectory.Exists)
        {
            outputDirectory.Create();
        }

        File.WriteAllText(output.FullName, rendered);
    }
}
