namespace DotnetCoupling.Core;

public sealed record Component(
    string Id,
    string Name,
    string Namespace,
    string? ProjectName,
    string FilePath,
    ComponentKind Kind,
    Visibility Visibility);

public enum ComponentKind
{
    Class,
    Record,
    Struct,
    Interface,
    Enum,
    Delegate,
    Namespace,
    Project,
    ExternalPackage,
}

public enum Visibility
{
    Public,
    Internal,
    Protected,
    ProtectedInternal,
    PrivateProtected,
    Private,
}

public enum UsageContext
{
    UsingDirective,
    Attribute,
    BaseType,
    InterfaceImplementation,
    GenericConstraint,
    FieldType,
    PropertyType,
    ParameterType,
    ReturnType,
    LocalVariableType,
    ObjectCreation,
    MethodCall,
    StaticCall,
    MemberAccess,
    FieldAccess,
    PropertyAccess,
    Reflection,
    DynamicDispatch,
    ServiceLocator,
}

public enum DependencyKind
{
    Using,
    TypeReference,
    Inheritance,
    InterfaceImplementation,
    GenericConstraint,
    ObjectCreation,
    MethodCall,
    StaticCall,
    FieldAccess,
    PropertyAccess,
    Attribute,
    Reflection,
    Dynamic,
}

public enum IntegrationStrength
{
    Contract,
    Model,
    Functional,
    Intrusive,
}

public enum Distance
{
    SameNamespace,
    DifferentNamespace,
    DifferentProject,
    ExternalPackage,
}

public enum Volatility
{
    Low,
    Medium,
    High,
}

public enum SubdomainCategory
{
    Core,
    Supporting,
    Generic,
}

public enum StrategicRole
{
    AnticorruptionLayer,
    PublishedLanguage,
    SharedKernel,
    OpenHostService,
}

public enum TechnicalRole
{
    DomainModel,
    ApplicationService,
    Adapter,
    CompositionRoot,
    Contract,
    TestSupport,
}

public enum IssueType
{
    GlobalComplexity,
    CascadingChangeRisk,
    InappropriateIntimacy,
    HighEfferentCoupling,
    HighAfferentCoupling,
    CircularDependency,
    HiddenCoupling,
    AccidentalVolatility,
    ScatteredExternalCoupling,
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical,
}

public sealed record AnalysisOptions(
    IReadOnlyList<string> ExcludePathPatterns,
    IReadOnlyList<string> TestProjectPathPatterns,
    IReadOnlyList<string> IgnorePathPatterns,
    IReadOnlyList<string> IgnoreNamespaces,
    IReadOnlySet<IssueType> IgnoreIssueTypes,
    IReadOnlyList<IssueSuppression> IssueSuppressions,
    AnalysisThresholds Thresholds,
    DomainContext DomainContext)
{
    public static AnalysisOptions Default { get; } = new(
        [],
        [],
        [],
        [],
        new HashSet<IssueType>(),
        [],
        AnalysisThresholds.Default,
        DomainContext.Empty);
}

public sealed record AnalysisThresholds(
    int MaxDependencies,
    int MaxDependents,
    int MinTemporalCoupling,
    int MaxTemporalFilesPerCommit,
    int ScatteredExternalBreadth)
{
    public static AnalysisThresholds Default { get; } = new(
        20,
        30,
        3,
        50,
        5);
}

public sealed record DomainContext(
    IReadOnlyList<DomainSubdomain> Subdomains,
    IReadOnlyList<DomainArea> Areas)
{
    public DomainContext(IReadOnlyList<DomainSubdomain> subdomains)
        : this(subdomains, [])
    {
    }

    public static DomainContext Empty { get; } = new([], []);
}

public sealed record DomainSubdomain(
    string Name,
    SubdomainCategory Category,
    IReadOnlyList<string> PathPatterns,
    Volatility ExpectedVolatility,
    StrategicRole? StrategicRole = null);

public sealed record DomainArea(
    string Name,
    IReadOnlyList<string> PathPatterns,
    TechnicalRole TechnicalRole);

public sealed record DomainContextSummary(
    int SubdomainCount,
    int MatchedComponents,
    int UnmatchedComponents,
    int AccidentalVolatilityIssues,
    IReadOnlyList<DomainSubdomainUsage> Subdomains,
    int AreaCount,
    int MatchedAreaComponents,
    int UnmatchedAreaComponents,
    IReadOnlyList<DomainAreaUsage> Areas)
{
    public DomainContextSummary(
        int SubdomainCount,
        int MatchedComponents,
        int UnmatchedComponents,
        int AccidentalVolatilityIssues,
        IReadOnlyList<DomainSubdomainUsage> Subdomains)
        : this(SubdomainCount, MatchedComponents, UnmatchedComponents, AccidentalVolatilityIssues, Subdomains, 0, 0, 0, [])
    {
    }
}

public sealed record DomainSubdomainUsage(
    string Name,
    SubdomainCategory Category,
    Volatility ExpectedVolatility,
    int MatchedComponents,
    StrategicRole? StrategicRole = null);

public sealed record DomainAreaUsage(
    string Name,
    TechnicalRole TechnicalRole,
    int MatchedComponents);

public sealed record ComponentRoleContext(
    string ComponentId,
    string FilePath,
    string? SubdomainName,
    StrategicRole? StrategicRole,
    string? AreaName,
    TechnicalRole? TechnicalRole);

public sealed record SourceLocation(string File, int Line);

public sealed record DependencyObservation(
    string SourceComponentId,
    string TargetName,
    DependencyKind Kind,
    UsageContext Usage,
    string FilePath,
    int Line,
    string? Expression);

public sealed record CouplingMetrics(
    string Source,
    string Target,
    IntegrationStrength Strength,
    Distance Distance,
    Volatility Volatility,
    string? SourceProject,
    string? TargetProject,
    Visibility TargetVisibility,
    SourceLocation Location);

public sealed record BalanceScore(
    CouplingMetrics Coupling,
    double Score,
    double Alignment,
    double VolatilityImpact,
    string Interpretation);

public sealed record CouplingIssue(
    IssueType Type,
    Severity Severity,
    string Source,
    string Target,
    double Score,
    string Problem,
    string Recommendation,
    SourceLocation? Location);

public sealed record IssueSuppression(
    IssueType Type,
    string Source,
    string Target,
    string Reason);

public sealed record SuppressedIssue(
    CouplingIssue Issue,
    string Reason);

public sealed record Hotspot(
    int Rank,
    string Component,
    double Score,
    int IssueCount,
    int FanIn,
    int FanOut,
    Volatility Volatility,
    bool CrossesBoundary,
    bool ParticipatesInCycle,
    IReadOnlyList<string> Reasons);

public sealed record TemporalCoupling(
    string FileA,
    string FileB,
    int CoChangeCount);

public sealed record AnalysisSummary(
    string Path,
    string Mode,
    int Files,
    int Components,
    int InternalCouplings,
    int ExternalCouplings,
    bool GitRequested,
    bool GitUsed,
    int GitMonths);

public sealed record AnalysisDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path);

public sealed record ProjectMetadata(
    int ProjectCount,
    IReadOnlyList<ProjectMetadataEntry> Projects);

public sealed record ProjectMetadataEntry(
    string ProjectPath,
    string ProjectName,
    string AssemblyName,
    int SourceFileCount,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);

public sealed record GradeResult(
    string Letter,
    string Display,
    string Basis,
    string Rationale);

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
    IReadOnlyList<ComponentRoleContext>? ComponentRoles = null);

public sealed record BaselineComparison(
    string Ref,
    IReadOnlyList<CouplingIssue> NewIssues,
    IReadOnlyList<CouplingIssue> ResolvedIssues,
    IReadOnlyList<CouplingIssue> UnchangedIssues);

public enum ReportFormat
{
    Text,
    Summary,
    Json,
    Hotspots,
}

public enum AnalysisMode
{
    Syntax,
    Semantic,
}
