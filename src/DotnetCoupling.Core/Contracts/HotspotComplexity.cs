namespace DotnetCoupling.Core;

public sealed record HotspotComplexity(
    int MaxCyclomaticComplexity,
    int MaxCognitiveComplexity,
    int TotalCyclomaticComplexity,
    int TotalCognitiveComplexity,
    int MemberCount,
    string? MostComplexMember,
    SourceLocation? Location);
