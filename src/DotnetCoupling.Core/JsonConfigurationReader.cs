using System.Text.Json;

namespace DotnetCoupling.Core;

internal static class JsonConfigurationReader
{
    internal static RawConfiguration Load(FileInfo configFile)
    {
        using FileStream stream = configFile.OpenRead();
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        ConfigurationValueReader.AssertKnownProperties(root, "", ["$schema", "analysis", "thresholds", "ignore", "domain"]);

        RawAnalysis? analysis = null;
        RawThresholds? thresholds = null;
        RawIgnore? ignore = null;
        RawDomain? domain = null;

        if (root.TryGetProperty("analysis", out JsonElement analysisElement))
        {
            ConfigurationValueReader.AssertKnownProperties(analysisElement, "analysis", ["exclude", "testProjects"]);
            analysis = new RawAnalysis(
                analysisElement.TryGetProperty("exclude", out JsonElement exclude)
                    ? ConfigurationValueReader.ReadStringArray(exclude, "analysis.exclude")
                    : null,
                analysisElement.TryGetProperty("testProjects", out JsonElement testProjects)
                    ? ConfigurationValueReader.ReadStringArray(testProjects, "analysis.testProjects")
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

        if (root.TryGetProperty("domain", out JsonElement domainElement))
        {
            ConfigurationValueReader.AssertKnownProperties(domainElement, "domain", ["subdomains"]);
            domain = new RawDomain(
                domainElement.TryGetProperty("subdomains", out JsonElement subdomains)
                    ? ReadDomainSubdomains(subdomains)
                    : null);
        }

        return new RawConfiguration(analysis, thresholds, ignore, domain);
    }

    private static List<RawDomainSubdomain> ReadDomainSubdomains(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigurationException("domain.subdomains must be an array.");
        }

        List<RawDomainSubdomain> subdomains = [];
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            string path = $"domain.subdomains[{index}]";
            ConfigurationValueReader.AssertKnownProperties(item, path, ["name", "category", "paths", "expectedVolatility"]);
            subdomains.Add(new RawDomainSubdomain(
                ConfigurationValueReader.ReadRequiredString(item, path, "name"),
                ConfigurationValueReader.ReadRequiredString(item, path, "category"),
                item.TryGetProperty("paths", out JsonElement paths)
                    ? ConfigurationValueReader.ReadStringArray(paths, $"{path}.paths")
                    : throw new ConfigurationException($"{path}.paths must be an array."),
                ConfigurationValueReader.ReadRequiredString(item, path, "expectedVolatility")));
            index++;
        }

        return subdomains;
    }
}
