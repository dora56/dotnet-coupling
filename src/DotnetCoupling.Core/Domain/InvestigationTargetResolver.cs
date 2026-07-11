namespace DotnetCoupling.Core;

internal static class InvestigationTargetResolver
{
    internal static Component ResolveComponent(IReadOnlyList<Component> components, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvestigationQueryException("Investigation query must not be empty.");
        }

        Component? exactIdentity = components.FirstOrDefault(component =>
            string.Equals(component.Id, query, StringComparison.Ordinal));
        if (exactIdentity is not null)
        {
            return exactIdentity;
        }

        Component[] exactSimpleName = components
            .Where(component => string.Equals(component.Name, query, StringComparison.Ordinal))
            .OrderBy(component => component.Id, StringComparer.Ordinal)
            .ToArray();
        if (exactSimpleName.Length == 1)
        {
            return exactSimpleName[0];
        }

        if (exactSimpleName.Length > 1)
        {
            throw CreateAmbiguousException(query, exactSimpleName.Select(component => component.Id));
        }

        Component[] caseInsensitiveMatches = components
            .Where(component =>
                string.Equals(component.Id, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(component.Name, query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(component => component.Id, StringComparer.Ordinal)
            .ToArray();
        if (caseInsensitiveMatches.Length == 1)
        {
            return caseInsensitiveMatches[0];
        }

        if (caseInsensitiveMatches.Length > 1)
        {
            throw CreateAmbiguousException(query, caseInsensitiveMatches.Select(component => component.Id));
        }

        string[] suggestions = components
            .Where(component =>
                component.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || component.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(component => component.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .Take(10)
            .ToArray();
        string suffix = suggestions.Length == 0
            ? ""
            : $" Candidates: {string.Join(", ", suggestions)}.";
        throw new InvestigationQueryException($"No analyzed component matches '{query}'.{suffix}");
    }

    private static InvestigationQueryException CreateAmbiguousException(
        string query,
        IEnumerable<string> candidates)
    {
        string candidateList = string.Join(", ", candidates.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
        return new InvestigationQueryException($"Investigation query '{query}' is ambiguous. Candidates: {candidateList}.");
    }
}
