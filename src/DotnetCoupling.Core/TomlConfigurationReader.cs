using Tomlyn;
using Tomlyn.Model;

namespace DotnetCoupling.Core;

internal static class TomlConfigurationReader
{
    internal static RawConfiguration Load(FileInfo configFile)
    {
        using StreamReader reader = configFile.OpenText();
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(reader) ?? new TomlTable();
        ConfigurationValueReader.AssertKnownProperties(root, "", ["analysis", "thresholds", "ignore"]);

        RawAnalysis? analysis = null;
        RawThresholds? thresholds = null;
        RawIgnore? ignore = null;

        if (root.TryGetValue("analysis", out object? analysisValue))
        {
            TomlTable analysisTable = ConfigurationValueReader.ReadTable(analysisValue, "analysis");
            ConfigurationValueReader.AssertKnownProperties(analysisTable, "analysis", ["exclude"]);
            analysis = new RawAnalysis(ConfigurationValueReader.ReadStringArray(analysisTable, "exclude", "analysis.exclude"));
        }

        if (root.TryGetValue("thresholds", out object? thresholdsValue))
        {
            TomlTable thresholdsTable = ConfigurationValueReader.ReadTable(thresholdsValue, "thresholds");
            ConfigurationValueReader.AssertKnownProperties(thresholdsTable, "thresholds", [
                "max_dependencies",
                "max_dependents",
                "min_temporal_coupling",
                "max_temporal_files_per_commit",
                "scattered_external_breadth",
            ]);
            thresholds = new RawThresholds(
                ConfigurationValueReader.ReadPositiveInt(thresholdsTable, "max_dependencies", "thresholds.max_dependencies"),
                ConfigurationValueReader.ReadPositiveInt(thresholdsTable, "max_dependents", "thresholds.max_dependents"),
                ConfigurationValueReader.ReadPositiveInt(thresholdsTable, "min_temporal_coupling", "thresholds.min_temporal_coupling"),
                ConfigurationValueReader.ReadPositiveInt(thresholdsTable, "max_temporal_files_per_commit", "thresholds.max_temporal_files_per_commit"),
                ConfigurationValueReader.ReadPositiveInt(thresholdsTable, "scattered_external_breadth", "thresholds.scattered_external_breadth"));
        }

        if (root.TryGetValue("ignore", out object? ignoreValue))
        {
            TomlTable ignoreTable = ConfigurationValueReader.ReadTable(ignoreValue, "ignore");
            ConfigurationValueReader.AssertKnownProperties(ignoreTable, "ignore", ["paths", "namespaces", "issue_types", "issues"]);
            ignore = new RawIgnore(
                ConfigurationValueReader.ReadStringArray(ignoreTable, "paths", "ignore.paths"),
                ConfigurationValueReader.ReadStringArray(ignoreTable, "namespaces", "ignore.namespaces"),
                ConfigurationValueReader.ReadStringArray(ignoreTable, "issue_types", "ignore.issue_types"),
                ConfigurationValueReader.ReadIssueSuppressions(ignoreTable, "issues"));
        }

        return new RawConfiguration(analysis, thresholds, ignore);
    }
}
