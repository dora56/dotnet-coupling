namespace DotnetCoupling.Core;

public sealed record ComponentComplexity(
    string ComponentId,
    string FilePath,
    int MemberCount,
    int MaxCyclomaticComplexity,
    int MaxCognitiveComplexity,
    int TotalCyclomaticComplexity,
    int TotalCognitiveComplexity,
    MemberComplexity? MostComplexMember);
