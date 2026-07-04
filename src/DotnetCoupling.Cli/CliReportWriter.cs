using DotnetCoupling.Core;
using DotnetCoupling.Sarif;

namespace DotnetCoupling.Cli;

internal sealed record CliReportRenderOptions(
    bool Summary,
    bool Json,
    bool Sarif,
    bool HotspotsRequested,
    bool Check,
    string AnalysisTargetPath);

internal static class CliReportWriter
{
    public static AnalysisReport AddHotspotsIfRequested(
        AnalysisReport report,
        bool hotspotsRequested,
        int? hotspotsValue)
    {
        if (!hotspotsRequested)
        {
            return report;
        }

        IReadOnlyList<Hotspot> hotspots = hotspotsValue is int hotspotCount
            ? HotspotAnalyzer.Calculate(report, hotspotCount)
            : HotspotAnalyzer.Calculate(report);
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

        ReportFormat format = options.HotspotsRequested
            ? ReportFormat.Hotspots
            : options.Summary || options.Check
                ? ReportFormat.Summary
                : ReportFormat.Text;
        return ReportRenderer.Render(report, format);
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
