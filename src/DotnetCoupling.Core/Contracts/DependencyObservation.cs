namespace DotnetCoupling.Core;

public sealed record DependencyObservation(
    string SourceComponentId,
    string TargetName,
    DependencyKind Kind,
    UsageContext Usage,
    string FilePath,
    int Line,
    string? Expression);
