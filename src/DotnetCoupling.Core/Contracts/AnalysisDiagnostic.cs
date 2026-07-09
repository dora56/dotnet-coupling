namespace DotnetCoupling.Core;

public sealed record AnalysisDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path);
