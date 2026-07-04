using Tomlyn;
using Tomlyn.Model;

namespace DotnetCoupling.Core;

internal static class TomlConfigurationReader
{
    internal static RawConfiguration Load(FileInfo configFile)
    {
        using StreamReader reader = configFile.OpenText();
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(reader) ?? new TomlTable();
        ConfigurationValueReader.AssertKnownProperties(root, "", ["analysis", "thresholds", "ignore", "domain"]);

        RawAnalysis? analysis = null;
        RawThresholds? thresholds = null;
        RawIgnore? ignore = null;
        RawDomain? domain = null;

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

        if (root.TryGetValue("domain", out object? domainValue))
        {
            TomlTable domainTable = ConfigurationValueReader.ReadTable(domainValue, "domain");
            ConfigurationValueReader.AssertKnownProperties(domainTable, "domain", ["subdomains"]);
            domain = new RawDomain(ReadDomainSubdomains(domainTable, "subdomains"));
        }

        return new RawConfiguration(analysis, thresholds, ignore, domain);
    }

    private static List<RawDomainSubdomain>? ReadDomainSubdomains(TomlTable table, string propertyName)
    {
        if (!table.TryGetValue(propertyName, out object? value))
        {
            return null;
        }

        if (value is not TomlTableArray array)
        {
            throw new ConfigurationException("domain.subdomains must be an array.");
        }

        List<RawDomainSubdomain> subdomains = [];
        int index = 0;
        foreach (TomlTable item in array)
        {
            string path = $"domain.subdomains[{index}]";
            ConfigurationValueReader.AssertKnownProperties(item, path, ["name", "category", "paths", "expected_volatility"]);
            subdomains.Add(new RawDomainSubdomain(
                ConfigurationValueReader.ReadRequiredString(item, path, "name"),
                ConfigurationValueReader.ReadRequiredString(item, path, "category"),
                ConfigurationValueReader.ReadStringArray(item, "paths", $"{path}.paths")
                    ?? throw new ConfigurationException($"{path}.paths must be an array."),
                ConfigurationValueReader.ReadRequiredString(item, path, "expected_volatility")));
            index++;
        }

        return subdomains;
    }
}
