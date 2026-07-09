namespace DotnetCoupling.Core;

public sealed record Component(
    string Id,
    string Name,
    string Namespace,
    string? ProjectName,
    string FilePath,
    ComponentKind Kind,
    Visibility Visibility);
