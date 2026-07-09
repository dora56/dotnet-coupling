namespace DotnetCoupling.Core;

public sealed record ComponentRoleContext(
    string ComponentId,
    string FilePath,
    string? SubdomainName,
    StrategicRole? StrategicRole,
    string? AreaName,
    TechnicalRole? TechnicalRole);
