namespace DotnetCoupling.Core;

public sealed record ProjectMetadata(
    int ProjectCount,
    IReadOnlyList<ProjectMetadataEntry> Projects);
