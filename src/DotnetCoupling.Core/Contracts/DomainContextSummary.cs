namespace DotnetCoupling.Core;

public sealed record DomainContextSummary(
    int SubdomainCount,
    int MatchedComponents,
    int UnmatchedComponents,
    int AccidentalVolatilityIssues,
    IReadOnlyList<DomainSubdomainUsage> Subdomains,
    int AreaCount,
    int MatchedAreaComponents,
    int UnmatchedAreaComponents,
    IReadOnlyList<DomainAreaUsage> Areas)
{
    public DomainContextSummary(
        int SubdomainCount,
        int MatchedComponents,
        int UnmatchedComponents,
        int AccidentalVolatilityIssues,
        IReadOnlyList<DomainSubdomainUsage> Subdomains)
        : this(SubdomainCount, MatchedComponents, UnmatchedComponents, AccidentalVolatilityIssues, Subdomains, 0, 0, 0, [])
    {
    }
}
