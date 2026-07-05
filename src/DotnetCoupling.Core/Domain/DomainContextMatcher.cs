namespace DotnetCoupling.Core;

internal static class DomainContextMatcher
{
    internal static DomainSubdomain? FindSubdomain(string filePath, DomainContext domainContext)
    {
        foreach (DomainSubdomain subdomain in domainContext.Subdomains)
        {
            if (PathPatternMatcher.IsMatch(filePath, subdomain.PathPatterns))
            {
                return subdomain;
            }
        }

        return null;
    }

    internal static DomainArea? FindArea(string filePath, DomainContext domainContext)
    {
        foreach (DomainArea area in domainContext.Areas)
        {
            if (PathPatternMatcher.IsMatch(filePath, area.PathPatterns))
            {
                return area;
            }
        }

        return null;
    }
}
