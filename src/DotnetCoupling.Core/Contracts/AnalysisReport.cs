namespace DotnetCoupling.Core;

public sealed record AnalysisReport(
    AnalysisSummary Summary,
    GradeResult Grade,
    double AverageBalanceScore,
    IReadOnlyList<Component> Components,
    IReadOnlyList<DependencyObservation> Observations,
    IReadOnlyList<CouplingMetrics> Couplings,
    IReadOnlyList<CouplingIssue> Issues,
    IReadOnlyList<string> BlindSpots,
    BaselineComparison? Baseline = null,
    IReadOnlyList<AnalysisDiagnostic>? Diagnostics = null,
    ProjectMetadata? ProjectMetadata = null,
    IReadOnlyList<SuppressedIssue>? SuppressedIssues = null,
    IReadOnlyList<Hotspot>? Hotspots = null,
    DomainContextSummary? DomainContext = null,
    IReadOnlyList<ComponentRoleContext>? ComponentRoles = null,
    IReadOnlyList<ComponentComplexity>? ComponentComplexities = null,
    ImpactAnalysis? Impact = null,
    TraceAnalysis? Trace = null);
