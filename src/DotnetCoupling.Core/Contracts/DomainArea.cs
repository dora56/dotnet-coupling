namespace DotnetCoupling.Core;

public sealed record DomainArea(
    string Name,
    IReadOnlyList<string> PathPatterns,
    TechnicalRole TechnicalRole);
