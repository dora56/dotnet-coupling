namespace DotnetCoupling.Core;

public sealed record DomainSubdomainUsage(
    string Name,
    SubdomainCategory Category,
    Volatility ExpectedVolatility,
    int MatchedComponents,
    StrategicRole? StrategicRole = null);
