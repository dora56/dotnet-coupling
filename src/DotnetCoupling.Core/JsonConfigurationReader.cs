using System.Text.Json;

namespace DotnetCoupling.Core;

internal static class JsonConfigurationReader
{
    internal static RawConfiguration Load(FileInfo configFile)
    {
        using FileStream stream = configFile.OpenRead();
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        ConfigurationValueReader.AssertKnownProperties(root, "", ["$schema", "analysis", "thresholds", "ignore"]);

        RawAnalysis? analysis = null;
        RawThresholds? thresholds = null;
        RawIgnore? ignore = null;

        if (root.TryGetProperty("analysis", out JsonElement analysisElement))
        {
            ConfigurationValueReader.AssertKnownProperties(analysisElement, "analysis", ["exclude"]);
            analysis = new RawAnalysis(
                analysisElement.TryGetProperty("exclude", out JsonElement exclude)
                    ? ConfigurationValueReader.ReadStringArray(exclude, "analysis.exclude")
                    : null);
        }

        if (root.TryGetProperty("thresholds", out JsonElement thresholdElement))
        {
            ConfigurationValueReader.AssertKnownProperties(thresholdElement, "thresholds", [
                "maxDependencies",
                "maxDependents",
                "minTemporalCoupling",
                "maxTemporalFilesPerCommit",
                "scatteredExternalBreadth",
            ]);
            thresholds = new RawThresholds(
                ConfigurationValueReader.ReadPositiveInt(thresholdElement, "maxDependencies"),
                ConfigurationValueReader.ReadPositiveInt(thresholdElement, "maxDependents"),
                ConfigurationValueReader.ReadPositiveInt(thresholdElement, "minTemporalCoupling"),
                ConfigurationValueReader.ReadPositiveInt(thresholdElement, "maxTemporalFilesPerCommit"),
                ConfigurationValueReader.ReadPositiveInt(thresholdElement, "scatteredExternalBreadth"));
        }

        if (root.TryGetProperty("ignore", out JsonElement ignoreElement))
        {
            ConfigurationValueReader.AssertKnownProperties(ignoreElement, "ignore", ["paths", "namespaces", "issueTypes", "issues"]);
            ignore = new RawIgnore(
                ignoreElement.TryGetProperty("paths", out JsonElement paths)
                    ? ConfigurationValueReader.ReadStringArray(paths, "ignore.paths")
                    : null,
                ignoreElement.TryGetProperty("namespaces", out JsonElement namespaces)
                    ? ConfigurationValueReader.ReadStringArray(namespaces, "ignore.namespaces")
                    : null,
                ignoreElement.TryGetProperty("issueTypes", out JsonElement issueTypes)
                    ? ConfigurationValueReader.ReadStringArray(issueTypes, "ignore.issueTypes")
                    : null,
                ignoreElement.TryGetProperty("issues", out JsonElement issues)
                    ? ConfigurationValueReader.ReadIssueSuppressions(issues)
                    : null);
        }

        return new RawConfiguration(analysis, thresholds, ignore);
    }
}
