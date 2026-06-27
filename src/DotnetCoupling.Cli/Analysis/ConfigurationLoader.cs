using System.Text.Json;

namespace DotnetCoupling.Cli.Analysis;

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public static ConfigurationLoadResult Load(string targetPath, FileInfo? explicitConfig)
    {
        FileInfo? configFile = explicitConfig ?? FindConfigFile(targetPath);
        if (configFile is null)
        {
            return new ConfigurationLoadResult(AnalysisOptions.Default, null, []);
        }

        if (!configFile.Exists)
        {
            throw new ConfigurationException($"Configuration file does not exist: {configFile.FullName}");
        }

        if (!string.Equals(configFile.Extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException($"Unsupported configuration file format: {configFile.FullName}");
        }

        try
        {
            using FileStream stream = configFile.OpenRead();
            using JsonDocument document = JsonDocument.Parse(stream);
            AssertKnownProperties(document.RootElement, "", ["$schema", "analysis", "thresholds", "ignore"]);

            AnalysisOptions defaults = AnalysisOptions.Default;
            List<string> excludePathPatterns = [.. defaults.ExcludePathPatterns];
            List<string> ignorePathPatterns = [.. defaults.IgnorePathPatterns];
            List<string> ignoreNamespaces = [.. defaults.IgnoreNamespaces];
            HashSet<IssueType> ignoreIssueTypes = new(defaults.IgnoreIssueTypes);
            AnalysisThresholds thresholds = defaults.Thresholds;

            if (document.RootElement.TryGetProperty("analysis", out JsonElement analysis))
            {
                AssertKnownProperties(analysis, "analysis", ["exclude"]);
                if (analysis.TryGetProperty("exclude", out JsonElement exclude))
                {
                    excludePathPatterns = ReadStringArray(exclude, "analysis.exclude");
                }
            }

            if (document.RootElement.TryGetProperty("thresholds", out JsonElement thresholdElement))
            {
                AssertKnownProperties(thresholdElement, "thresholds", [
                    "maxDependencies",
                    "maxDependents",
                    "minTemporalCoupling",
                    "maxTemporalFilesPerCommit",
                    "scatteredExternalBreadth",
                ]);
                thresholds = new AnalysisThresholds(
                    ReadPositiveInt(thresholdElement, "maxDependencies", thresholds.MaxDependencies),
                    ReadPositiveInt(thresholdElement, "maxDependents", thresholds.MaxDependents),
                    ReadPositiveInt(thresholdElement, "minTemporalCoupling", thresholds.MinTemporalCoupling),
                    ReadPositiveInt(thresholdElement, "maxTemporalFilesPerCommit", thresholds.MaxTemporalFilesPerCommit),
                    ReadPositiveInt(thresholdElement, "scatteredExternalBreadth", thresholds.ScatteredExternalBreadth));
            }

            if (document.RootElement.TryGetProperty("ignore", out JsonElement ignore))
            {
                AssertKnownProperties(ignore, "ignore", ["paths", "namespaces", "issueTypes"]);
                if (ignore.TryGetProperty("paths", out JsonElement paths))
                {
                    ignorePathPatterns = ReadStringArray(paths, "ignore.paths");
                }

                if (ignore.TryGetProperty("namespaces", out JsonElement namespaces))
                {
                    ignoreNamespaces = ReadStringArray(namespaces, "ignore.namespaces");
                }

                if (ignore.TryGetProperty("issueTypes", out JsonElement issueTypes))
                {
                    ignoreIssueTypes = ReadIssueTypes(issueTypes);
                }
            }

            AnalysisOptions loaded = new(
                excludePathPatterns,
                ignorePathPatterns,
                ignoreNamespaces,
                ignoreIssueTypes,
                thresholds);
            return new ConfigurationLoadResult(loaded, configFile.FullName, []);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Invalid JSON configuration: {ex.Message}");
        }
    }

    private static FileInfo? FindConfigFile(string targetPath)
    {
        DirectoryInfo? directory = File.Exists(targetPath)
            ? new FileInfo(targetPath).Directory
            : new DirectoryInfo(targetPath);

        while (directory is not null)
        {
            FileInfo dotConfig = new(Path.Combine(directory.FullName, ".coupling.json"));
            if (dotConfig.Exists)
            {
                return dotConfig;
            }

            FileInfo config = new(Path.Combine(directory.FullName, "coupling.json"));
            if (config.Exists)
            {
                return config;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void AssertKnownProperties(JsonElement element, string path, IReadOnlyCollection<string> knownProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException($"{(path.Length == 0 ? "configuration" : path)} must be an object.");
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                string prefix = path.Length == 0 ? "" : path + ".";
                throw new ConfigurationException($"Unknown configuration property: {prefix}{property.Name}");
            }
        }
    }

    private static List<string> ReadStringArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigurationException($"{path} must be an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new ConfigurationException($"{path} must contain non-empty strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static int ReadPositiveInt(JsonElement element, string propertyName, int fallback)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result <= 0)
        {
            throw new ConfigurationException($"thresholds.{propertyName} must be a positive integer.");
        }

        return result;
    }

    private static HashSet<IssueType> ReadIssueTypes(JsonElement element)
    {
        HashSet<IssueType> values = [];
        foreach (string value in ReadStringArray(element, "ignore.issueTypes"))
        {
            if (!Enum.TryParse(value, ignoreCase: true, out IssueType issueType))
            {
                throw new ConfigurationException($"Invalid issue type in ignore.issueTypes: {value}");
            }

            values.Add(issueType);
        }

        return values;
    }
}

public sealed record ConfigurationLoadResult(
    AnalysisOptions Options,
    string? ConfigPath,
    IReadOnlyList<string> Warnings);

public sealed class ConfigurationException(string message) : Exception(message);
