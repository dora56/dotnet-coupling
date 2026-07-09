namespace DotnetCoupling.Core;

public sealed record DomainAreaUsage(
    string Name,
    TechnicalRole TechnicalRole,
    int MatchedComponents);
