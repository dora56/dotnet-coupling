namespace DotnetCoupling.Core;

public sealed record ProjectMetadataEntry(
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    int SourceFileCount,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);
