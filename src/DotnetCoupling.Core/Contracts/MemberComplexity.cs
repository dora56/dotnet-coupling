namespace DotnetCoupling.Core;

public sealed record MemberComplexity(
    string ComponentId,
    string MemberName,
    SourceLocation Location,
    int CyclomaticComplexity,
    int CognitiveComplexity);
