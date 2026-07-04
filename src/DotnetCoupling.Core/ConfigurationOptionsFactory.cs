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
            rawIgnore?.PathPatterns ?? defaults.IgnorePathPatterns,
            rawIgnore?.Namespaces ?? defaults.IgnoreNamespaces,
            rawIgnore?.IssueTypes is null ? defaults.IgnoreIssueTypes : ReadIssueTypes(rawIgnore.IssueTypes),
            rawIgnore?.Issues is null ? defaults.IssueSuppressions : ReadIssueSuppressions(rawIgnore.Issues),
            thresholds);
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
}
