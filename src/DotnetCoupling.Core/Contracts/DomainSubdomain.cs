namespace DotnetCoupling.Core;

public sealed record DomainSubdomain(
    string Name,
    SubdomainCategory Category,
    IReadOnlyList<string> PathPatterns,
    Volatility ExpectedVolatility,
    StrategicRole? StrategicRole = null);
