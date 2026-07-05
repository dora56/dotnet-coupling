namespace DotnetCoupling.Core;

internal static class ConfigurationOptionsFactory
{
    internal static AnalysisOptions Create(RawConfiguration configuration)
    {
        AnalysisOptions defaults = AnalysisOptions.Default;
        AnalysisThresholds defaultThresholds = defaults.Thresholds;
        RawThresholds? rawThresholds = configuration.Thresholds;
        AnalysisThresholds thresholds = new(
            rawThresholds?.MaxDependencies ?? defaultThresholds.MaxDependencies,
            rawThresholds?.MaxDependents ?? defaultThresholds.MaxDependents,
            rawThresholds?.MinTemporalCoupling ?? defaultThresholds.MinTemporalCoupling,
            rawThresholds?.MaxTemporalFilesPerCommit ?? defaultThresholds.MaxTemporalFilesPerCommit,
            rawThresholds?.ScatteredExternalBreadth ?? defaultThresholds.ScatteredExternalBreadth);

        RawIgnore? rawIgnore = configuration.Ignore;
        return new AnalysisOptions(
            configuration.Analysis?.ExcludePathPatterns ?? defaults.ExcludePathPatterns,
            configuration.Analysis?.TestProjectPathPatterns ?? defaults.TestProjectPathPatterns,
            rawIgnore?.PathPatterns ?? defaults.IgnorePathPatterns,
            rawIgnore?.Namespaces ?? defaults.IgnoreNamespaces,
            rawIgnore?.IssueTypes is null ? defaults.IgnoreIssueTypes : ReadIssueTypes(rawIgnore.IssueTypes),
            rawIgnore?.Issues is null ? defaults.IssueSuppressions : ReadIssueSuppressions(rawIgnore.Issues),
            thresholds,
            configuration.Domain is null ? defaults.DomainContext : ReadDomainContext(configuration.Domain));
    }

    private static HashSet<IssueType> ReadIssueTypes(IReadOnlyList<string> rawValues)
    {
        HashSet<IssueType> values = [];
        foreach (string value in rawValues)
        {
            if (!Enum.TryParse(value, ignoreCase: true, out IssueType issueType))
            {
                throw new ConfigurationException($"Invalid issue type in ignore.issueTypes: {value}");
            }

            values.Add(issueType);
        }

        return values;
    }

    private static List<IssueSuppression> ReadIssueSuppressions(IReadOnlyList<RawIssueSuppression> rawSuppressions)
    {
        List<IssueSuppression> suppressions = [];
        int index = 0;
        foreach (RawIssueSuppression suppression in rawSuppressions)
        {
            string path = $"ignore.issues[{index}]";
            if (!Enum.TryParse(suppression.Type, ignoreCase: true, out IssueType issueType))
            {
                throw new ConfigurationException($"Invalid issue type in {path}.type: {suppression.Type}");
            }

            suppressions.Add(new IssueSuppression(issueType, suppression.Source, suppression.Target, suppression.Reason));
            index++;
        }

        return suppressions;
    }

    private static DomainContext ReadDomainContext(RawDomain rawDomain)
    {
        if (rawDomain.Subdomains is null && rawDomain.Areas is null)
        {
            return DomainContext.Empty;
        }

        List<DomainSubdomain> subdomains = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (RawDomainSubdomain rawSubdomain in rawDomain.Subdomains ?? [])
        {
            string path = $"domain.subdomains[{index}]";
            if (!TryParseConfigurationEnum(rawSubdomain.Category, out SubdomainCategory category))
            {
                throw new ConfigurationException($"Invalid subdomain category in {rawSubdomain.CategoryPath}: {rawSubdomain.Category}");
            }

            if (!TryParseConfigurationEnum(rawSubdomain.ExpectedVolatility, out Volatility expectedVolatility))
            {
                throw new ConfigurationException($"Invalid volatility in {rawSubdomain.ExpectedVolatilityPath}: {rawSubdomain.ExpectedVolatility}");
            }

            StrategicRole? strategicRole = null;
            if (!string.IsNullOrWhiteSpace(rawSubdomain.StrategicRole))
            {
                if (!TryParseConfigurationEnum(rawSubdomain.StrategicRole, out StrategicRole parsedStrategicRole))
                {
                    throw new ConfigurationException($"Invalid strategic role in {rawSubdomain.StrategicRolePath}: {rawSubdomain.StrategicRole}");
                }

                strategicRole = parsedStrategicRole;
            }

            if (rawSubdomain.PathPatterns.Count == 0)
            {
                throw new ConfigurationException($"{path}.paths must contain at least one path pattern.");
            }

            if (!names.Add(rawSubdomain.Name))
            {
                throw new ConfigurationException($"Duplicate domain subdomain name in {path}.name: {rawSubdomain.Name}");
            }

            subdomains.Add(new DomainSubdomain(
                rawSubdomain.Name,
                category,
                rawSubdomain.PathPatterns,
                expectedVolatility,
                strategicRole));
            index++;
        }

        List<DomainArea> areas = [];
        HashSet<string> areaNames = new(StringComparer.OrdinalIgnoreCase);
        index = 0;
        foreach (RawDomainArea rawArea in rawDomain.Areas ?? [])
        {
            string path = $"domain.areas[{index}]";
            if (!TryParseConfigurationEnum(rawArea.TechnicalRole, out TechnicalRole technicalRole))
            {
                throw new ConfigurationException($"Invalid technical role in {rawArea.TechnicalRolePath}: {rawArea.TechnicalRole}");
            }

            if (rawArea.PathPatterns.Count == 0)
            {
                throw new ConfigurationException($"{path}.paths must contain at least one path pattern.");
            }

            if (!areaNames.Add(rawArea.Name))
            {
                throw new ConfigurationException($"Duplicate domain area name in {path}.name: {rawArea.Name}");
            }

            areas.Add(new DomainArea(rawArea.Name, rawArea.PathPatterns, technicalRole));
            index++;
        }

        return new DomainContext(subdomains, areas);
    }

    private static bool TryParseConfigurationEnum<TEnum>(string value, out TEnum result)
        where TEnum : struct, Enum
    {
        string normalizedValue = NormalizeEnumValue(value);
        foreach (string name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(NormalizeEnumValue(name), normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                result = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        result = default;
        return false;
    }

    private static string NormalizeEnumValue(string value)
    {
        return string.Concat(value.Where(character => character is not '_' and not '-'));
    }
}
