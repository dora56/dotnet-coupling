namespace DotnetCoupling.Core;

public sealed record DomainContext(
    IReadOnlyList<DomainSubdomain> Subdomains,
    IReadOnlyList<DomainArea> Areas)
{
    public DomainContext(IReadOnlyList<DomainSubdomain> subdomains)
        : this(subdomains, [])
    {
    }

    public static DomainContext Empty { get; } = new([], []);
}
