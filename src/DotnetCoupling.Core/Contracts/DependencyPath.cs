namespace DotnetCoupling.Core;

public sealed record DependencyPath(
    string Component,
    int Depth,
    IReadOnlyList<string> Path,
    string? Project,
    string Namespace,
    bool CrossesBoundary,
    SourceLocation? Location,
    string? SourceSymbol = null);
