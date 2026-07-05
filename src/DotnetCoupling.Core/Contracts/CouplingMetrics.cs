namespace DotnetCoupling.Core;

public sealed record CouplingMetrics(
    string Source,
    string Target,
    IntegrationStrength Strength,
    Distance Distance,
    Volatility Volatility,
    string? SourceProject,
    string? TargetProject,
    Visibility TargetVisibility,
    SourceLocation Location);
