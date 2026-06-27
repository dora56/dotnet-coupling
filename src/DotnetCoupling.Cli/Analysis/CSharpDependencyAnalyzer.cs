using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Cli.Analysis;

public sealed class CSharpDependencyAnalyzer
{
    private static readonly string[] ExcludedDirectoryNames = [".git", ".vs", "bin", "obj"];

    public static AnalysisReport Analyze(string targetPath, bool useGit, int gitMonths)
    {
        string fullPath = Path.GetFullPath(targetPath);
        string[] files = DiscoverCSharpFiles(fullPath);
        List<Component> components = [];
        List<DependencyObservation> observations = [];
        Dictionary<string, List<UsingNamespace>> usingNamespacesByFile = new(StringComparer.Ordinal);

        foreach (string file in files)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            usingNamespacesByFile[file] = CollectUsingNamespaces(tree, root);
            string namespaceName = FindNamespace(root);
            ComponentWalker walker = new(tree, namespaceName, file);
            walker.Visit(root);
            components.AddRange(walker.Components);
            observations.AddRange(walker.Observations);
        }

        Dictionary<string, Component> componentsByName = components
            .GroupBy(component => component.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, Component> componentsById = components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        HashSet<string> internalNamespaces = components
            .Select(component => component.Namespace)
            .Where(namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> analyzedFiles = files.ToHashSet(StringComparer.Ordinal);

        IReadOnlyDictionary<string, int> changeCounts = useGit
            ? GitVolatility.GetChangeCounts(fullPath, gitMonths)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<TemporalCoupling> temporalCouplings = useGit
            ? GitVolatility.GetTemporalCouplings(fullPath, gitMonths, analyzedFiles)
            : [];

        List<CouplingMetrics> couplings = [];
        foreach (DependencyObservation observation in observations)
        {
            if (!componentsByName.TryGetValue(observation.TargetName, out Component? target))
            {
                continue;
            }

            if (observation.SourceComponentId == target.Id)
            {
                continue;
            }

            if (!componentsById.TryGetValue(observation.SourceComponentId, out Component? source))
            {
                continue;
            }

            Distance distance = ResolveDistance(source, target);
            Volatility volatility = ResolveVolatility(target.FilePath, changeCounts);
            couplings.Add(new CouplingMetrics(
                source.Id,
                target.Id,
                ResolveStrength(observation, target),
                distance,
                volatility,
                source.ProjectName,
                target.ProjectName,
                target.Visibility,
                new SourceLocation(observation.FilePath, observation.Line)));
        }

        AddExternalUsingCouplings(couplings, components, usingNamespacesByFile, internalNamespaces);

        List<BalanceScore> scores = couplings.Select(CouplingScoring.Calculate).ToList();
        int internalCouplingCount = couplings.Count(coupling => coupling.Distance != Distance.ExternalPackage);
        int externalCouplingCount = couplings.Count - internalCouplingCount;
        List<CouplingIssue> issues = DetectIssues(scores, temporalCouplings, componentsById);
        GradeResult grade = CouplingScoring.CalculateGrade(internalCouplingCount, issues);
        AnalysisSummary summary = new(
            fullPath,
            "syntax-only",
            files.Length,
            components.Count,
            internalCouplingCount,
            externalCouplingCount,
            useGit && changeCounts.Count > 0,
            gitMonths);

        return new AnalysisReport(
            summary,
            grade,
            scores.Count == 0 ? 1.0 : scores.Average(score => score.Score),
            components,
            observations,
            couplings,
            issues,
            [
                "Semantic symbol resolution is not enabled.",
                "DI container runtime resolution is not analyzed in syntax-only mode.",
                "Reflection and dynamic calls may be incomplete.",
                "Generated code is excluded by default.",
            ]);
    }

    private static string[] DiscoverCSharpFiles(string fullPath)
    {
        if (File.Exists(fullPath) && fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return [fullPath];
        }

        if (!Directory.Exists(fullPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsExcludedPath(file) && !IsGeneratedFile(file))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExcludedPath(string file)
    {
        string[] segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsGeneratedFile(string file)
    {
        string fileName = Path.GetFileName(file);
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        using StreamReader reader = File.OpenText(file);
        for (int i = 0; i < 5 && !reader.EndOfStream; i++)
        {
            string? line = reader.ReadLine();
            if (line is not null && line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindNamespace(CompilationUnitSyntax root)
    {
        FileScopedNamespaceDeclarationSyntax? fileScoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScoped is not null)
        {
            return fileScoped.Name.ToString();
        }

        NamespaceDeclarationSyntax? blockScoped = root.Members.OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return blockScoped?.Name.ToString() ?? "";
    }

    private static List<UsingNamespace> CollectUsingNamespaces(SyntaxTree tree, CompilationUnitSyntax root)
    {
        List<UsingNamespace> usingNamespaces = [];
        foreach (UsingDirectiveSyntax usingDirective in root.Usings)
        {
            if (usingDirective.Alias is not null || usingDirective.Name is null)
            {
                continue;
            }

            FileLinePositionSpan span = tree.GetLineSpan(usingDirective.Span);
            usingNamespaces.Add(new UsingNamespace(
                usingDirective.Name.ToString(),
                span.StartLinePosition.Line + 1));
        }

        return usingNamespaces;
    }

    private static IntegrationStrength ResolveStrength(DependencyObservation observation, Component target)
    {
        if (target.Kind == ComponentKind.Interface)
        {
            return IntegrationStrength.Contract;
        }

        return observation.Usage switch
        {
            UsageContext.BaseType or UsageContext.InterfaceImplementation or UsageContext.GenericConstraint => IntegrationStrength.Contract,
            UsageContext.ObjectCreation or UsageContext.MethodCall or UsageContext.StaticCall => IntegrationStrength.Functional,
            UsageContext.FieldAccess or UsageContext.Reflection or UsageContext.DynamicDispatch => IntegrationStrength.Intrusive,
            _ => IntegrationStrength.Model,
        };
    }

    private static Distance ResolveDistance(Component source, Component target)
    {
        if (source.Namespace == target.Namespace)
        {
            return Distance.SameNamespace;
        }

        int sharedSegments = source.Namespace
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Zip(target.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .TakeWhile(pair => pair.First == pair.Second)
            .Count();

        return sharedSegments >= 2 ? Distance.DifferentNamespace : Distance.DifferentProject;
    }

    private static Volatility ResolveVolatility(string filePath, IReadOnlyDictionary<string, int> changeCounts)
    {
        if (!changeCounts.TryGetValue(filePath, out int changes))
        {
            return Volatility.Low;
        }

        return changes switch
        {
            <= 2 => Volatility.Low,
            <= 10 => Volatility.Medium,
            _ => Volatility.High,
        };
    }

    private static void AddExternalUsingCouplings(
        List<CouplingMetrics> couplings,
        IReadOnlyCollection<Component> components,
        IReadOnlyDictionary<string, List<UsingNamespace>> usingNamespacesByFile,
        IReadOnlySet<string> internalNamespaces)
    {
        foreach (IGrouping<string, Component> fileComponents in components.GroupBy(component => component.FilePath, StringComparer.Ordinal))
        {
            if (!usingNamespacesByFile.TryGetValue(fileComponents.Key, out List<UsingNamespace>? usingNamespaces))
            {
                continue;
            }

            foreach (UsingNamespace usingNamespace in usingNamespaces)
            {
                if (IsFrameworkUsing(usingNamespace.Name) || IsInternalUsing(usingNamespace.Name, internalNamespaces))
                {
                    continue;
                }

                string packageName = NormalizeExternalPackageName(usingNamespace.Name);
                foreach (Component source in fileComponents)
                {
                    couplings.Add(new CouplingMetrics(
                        source.Id,
                        packageName,
                        IntegrationStrength.Contract,
                        Distance.ExternalPackage,
                        Volatility.Low,
                        source.ProjectName,
                        packageName,
                        Visibility.Public,
                        new SourceLocation(source.FilePath, usingNamespace.Line)));
                }
            }
        }
    }

    private static bool IsFrameworkUsing(string namespaceName)
    {
        return namespaceName == "System"
            || namespaceName.StartsWith("System.", StringComparison.Ordinal)
            || namespaceName == "Microsoft"
            || namespaceName.StartsWith("Microsoft.", StringComparison.Ordinal);
    }

    private static bool IsInternalUsing(string namespaceName, IReadOnlySet<string> internalNamespaces)
    {
        return internalNamespaces.Any(internalNamespace =>
            namespaceName == internalNamespace
            || namespaceName.StartsWith(internalNamespace + ".", StringComparison.Ordinal)
            || internalNamespace.StartsWith(namespaceName + ".", StringComparison.Ordinal));
    }

    private static string NormalizeExternalPackageName(string namespaceName)
    {
        string[] segments = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            return $"{segments[0]}.{segments[1]}";
        }

        return segments.FirstOrDefault() ?? namespaceName;
    }

    private static List<CouplingIssue> DetectIssues(
        IReadOnlyCollection<BalanceScore> scores,
        IReadOnlyList<TemporalCoupling> temporalCouplings,
        IReadOnlyDictionary<string, Component> componentsById)
    {
        List<CouplingIssue> issues = [];

        foreach (BalanceScore score in scores)
        {
            CouplingMetrics coupling = score.Coupling;
            if (coupling.Strength >= IntegrationStrength.Functional && coupling.Distance >= Distance.DifferentNamespace)
            {
                issues.Add(new CouplingIssue(
                    IssueType.GlobalComplexity,
                    score.Score < 0.40 ? Severity.High : Severity.Medium,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Strong coupling spans a namespace or project boundary.",
                    "Introduce an interface, move the dependency closer, or add a port/adapter.",
                    coupling.Location));
            }

            if (coupling.Strength >= IntegrationStrength.Functional && coupling.Volatility == Volatility.High)
            {
                issues.Add(new CouplingIssue(
                    IssueType.CascadingChangeRisk,
                    Severity.High,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Strong coupling targets a frequently changing component.",
                    "Stabilize the target API, introduce an interface, or invert the dependency.",
                    coupling.Location));
            }

            if (coupling.Strength == IntegrationStrength.Intrusive && coupling.Distance >= Distance.DifferentNamespace)
            {
                issues.Add(new CouplingIssue(
                    IssueType.InappropriateIntimacy,
                    Severity.High,
                    coupling.Source,
                    coupling.Target,
                    score.Score,
                    "Intrusive implementation-detail access crosses a boundary.",
                    "Encapsulate the implementation detail behind a stable API.",
                    coupling.Location));
            }
        }

        AddFanInFanOutIssues(scores.Select(score => score.Coupling), issues);
        AddCircularDependencyIssues(scores.Select(score => score.Coupling), issues);
        AddHiddenCouplingIssues(temporalCouplings, scores.Select(score => score.Coupling), componentsById, issues);
        AddScatteredExternalCouplingIssues(scores.Select(score => score.Coupling), issues);
        return issues;
    }

    private static void AddFanInFanOutIssues(IEnumerable<CouplingMetrics> couplings, List<CouplingIssue> issues)
    {
        const int maxDependencies = 20;
        const int maxDependents = 30;

        foreach (IGrouping<string, CouplingMetrics> group in couplings.GroupBy(coupling => coupling.Source))
        {
            int count = group.Select(coupling => coupling.Target).Distinct(StringComparer.Ordinal).Count();
            if (count > maxDependencies)
            {
                issues.Add(new CouplingIssue(
                    IssueType.HighEfferentCoupling,
                    count > maxDependencies * 2 ? Severity.High : Severity.Medium,
                    group.Key,
                    "",
                    1.0,
                    "Component depends on too many distinct targets.",
                    "Split responsibilities or extract focused sub-modules.",
                    null));
            }
        }

        foreach (IGrouping<string, CouplingMetrics> group in couplings.GroupBy(coupling => coupling.Target))
        {
            int count = group.Select(coupling => coupling.Source).Distinct(StringComparer.Ordinal).Count();
            if (count > maxDependents)
            {
                issues.Add(new CouplingIssue(
                    IssueType.HighAfferentCoupling,
                    count > maxDependents * 2 ? Severity.High : Severity.Medium,
                    "",
                    group.Key,
                    1.0,
                    "Component has too many distinct dependents.",
                    "Stabilize the public API or split the shared model.",
                    null));
            }
        }
    }

    private static void AddCircularDependencyIssues(IEnumerable<CouplingMetrics> couplings, List<CouplingIssue> issues)
    {
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);
        foreach (CouplingMetrics coupling in couplings)
        {
            string sourceNamespace = NamespaceOf(coupling.Source);
            string targetNamespace = NamespaceOf(coupling.Target);
            if (string.IsNullOrWhiteSpace(sourceNamespace) || sourceNamespace == targetNamespace)
            {
                continue;
            }

            if (!graph.TryGetValue(sourceNamespace, out HashSet<string>? targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                graph[sourceNamespace] = targets;
            }

            targets.Add(targetNamespace);
            graph.TryAdd(targetNamespace, new HashSet<string>(StringComparer.Ordinal));
        }

        foreach (IReadOnlyCollection<string> component in FindStronglyConnectedComponents(graph))
        {
            if (component.Count <= 1)
            {
                continue;
            }

            string source = string.Join(" -> ", component.Order(StringComparer.Ordinal));
            issues.Add(new CouplingIssue(
                IssueType.CircularDependency,
                Severity.High,
                source,
                source,
                0.0,
                "Namespaces form a circular dependency.",
                "Invert one direction via an interface, extract a shared contract, or use events.",
                null));
        }
    }

    private static void AddHiddenCouplingIssues(
        IReadOnlyList<TemporalCoupling> temporalCouplings,
        IEnumerable<CouplingMetrics> couplings,
        IReadOnlyDictionary<string, Component> componentsById,
        List<CouplingIssue> issues)
    {
        HashSet<(string FileA, string FileB)> explicitFilePairs = new();
        foreach (CouplingMetrics coupling in couplings)
        {
            if (!componentsById.TryGetValue(coupling.Source, out Component? source)
                || !componentsById.TryGetValue(coupling.Target, out Component? target))
            {
                continue;
            }

            if (source.FilePath == target.FilePath)
            {
                continue;
            }

            explicitFilePairs.Add(OrderPair(source.FilePath, target.FilePath));
        }

        foreach (TemporalCoupling temporalCoupling in temporalCouplings)
        {
            (string FileA, string FileB) pair = OrderPair(temporalCoupling.FileA, temporalCoupling.FileB);
            if (explicitFilePairs.Contains(pair))
            {
                continue;
            }

            issues.Add(new CouplingIssue(
                IssueType.HiddenCoupling,
                temporalCoupling.CoChangeCount >= 6 ? Severity.High : Severity.Medium,
                temporalCoupling.FileA,
                temporalCoupling.FileB,
                0.50,
                $"Files changed together {temporalCoupling.CoChangeCount} times without an explicit code dependency.",
                "Review whether a shared concept should be extracted, duplicated logic unified, or the dependency made explicit.",
                null));
        }
    }

    private static void AddScatteredExternalCouplingIssues(IEnumerable<CouplingMetrics> couplings, List<CouplingIssue> issues)
    {
        const int scatteredExternalBreadth = 5;

        foreach (IGrouping<string, CouplingMetrics> group in couplings
            .Where(coupling => coupling.Distance == Distance.ExternalPackage)
            .GroupBy(coupling => coupling.Target, StringComparer.Ordinal))
        {
            int directUsers = group.Select(coupling => coupling.Source).Distinct(StringComparer.Ordinal).Count();
            if (directUsers < scatteredExternalBreadth)
            {
                continue;
            }

            issues.Add(new CouplingIssue(
                IssueType.ScatteredExternalCoupling,
                Severity.Medium,
                "",
                group.Key,
                0.50,
                $"External package namespace is used directly by {directUsers} internal components.",
                "Introduce a wrapper or adapter so upgrade risk is concentrated behind an internal API.",
                null));
        }
    }

    private static (string FileA, string FileB) OrderPair(string first, string second)
    {
        return string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
    }

    private static string NamespaceOf(string componentId)
    {
        int lastDot = componentId.LastIndexOf('.');
        return lastDot < 0 ? "" : componentId[..lastDot];
    }

    private static List<IReadOnlyCollection<string>> FindStronglyConnectedComponents(Dictionary<string, HashSet<string>> graph)
    {
        int index = 0;
        Stack<string> stack = new();
        HashSet<string> onStack = new(StringComparer.Ordinal);
        Dictionary<string, int> indexes = new(StringComparer.Ordinal);
        Dictionary<string, int> lowLinks = new(StringComparer.Ordinal);
        List<IReadOnlyCollection<string>> components = [];

        foreach (string node in graph.Keys.Order(StringComparer.Ordinal))
        {
            if (!indexes.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        return components;

        void StrongConnect(string node)
        {
            indexes[node] = index;
            lowLinks[node] = index;
            index++;
            stack.Push(node);
            onStack.Add(node);

            foreach (string target in graph[node].Order(StringComparer.Ordinal))
            {
                if (!indexes.ContainsKey(target))
                {
                    StrongConnect(target);
                    lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                }
                else if (onStack.Contains(target) && indexes.TryGetValue(target, out int targetIndex))
                {
                    lowLinks[node] = Math.Min(lowLinks[node], targetIndex);
                }
            }

            if (lowLinks[node] != indexes[node])
            {
                return;
            }

            List<string> component = [];
            string current;
            do
            {
                current = stack.Pop();
                onStack.Remove(current);
                component.Add(current);
            }
            while (current != node);

            components.Add(component);
        }
    }

    private sealed class ComponentWalker(SyntaxTree tree, string namespaceName, string filePath) : CSharpSyntaxWalker
    {
        private readonly Stack<Component> _componentStack = new();

        public List<Component> Components { get; } = [];

        public List<DependencyObservation> Observations { get; } = [];

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, ComponentKind.Class, () => base.VisitClassDeclaration(node));
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, ComponentKind.Record, () => base.VisitRecordDeclaration(node));
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, ComponentKind.Struct, () => base.VisitStructDeclaration(node));
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, ComponentKind.Interface, () => base.VisitInterfaceDeclaration(node));
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            Component component = CreateComponent(node.Identifier.ValueText, ComponentKind.Enum, node.Modifiers);
            Components.Add(component);
        }

        public override void VisitBaseList(BaseListSyntax node)
        {
            foreach (BaseTypeSyntax baseType in node.Types)
            {
                AddObservation(baseType.Type, DependencyKind.Inheritance, UsageContext.BaseType);
            }

            base.VisitBaseList(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            AddObservation(node.Declaration.Type, DependencyKind.TypeReference, UsageContext.FieldType);
            base.VisitFieldDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AddObservation(node.Type, DependencyKind.TypeReference, UsageContext.PropertyType);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AddObservation(node.ReturnType, DependencyKind.TypeReference, UsageContext.ReturnType);
            foreach (ParameterSyntax parameter in node.ParameterList.Parameters)
            {
                if (parameter.Type is not null)
                {
                    AddObservation(parameter.Type, DependencyKind.TypeReference, UsageContext.ParameterType);
                }
            }

            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            foreach (ParameterSyntax parameter in node.ParameterList.Parameters)
            {
                if (parameter.Type is not null)
                {
                    AddObservation(parameter.Type, DependencyKind.TypeReference, UsageContext.ParameterType);
                }
            }

            base.VisitConstructorDeclaration(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            AddObservation(node.Type, DependencyKind.ObjectCreation, UsageContext.ObjectCreation);
            base.VisitObjectCreationExpression(node);
        }

        private void VisitTypeDeclaration(TypeDeclarationSyntax node, ComponentKind kind, Action visitChildren)
        {
            Component component = CreateComponent(node.Identifier.ValueText, kind, node.Modifiers);
            Components.Add(component);
            _componentStack.Push(component);
            visitChildren();
            _componentStack.Pop();
        }

        private Component CreateComponent(string name, ComponentKind kind, SyntaxTokenList modifiers)
        {
            string id = string.IsNullOrWhiteSpace(namespaceName) ? name : $"{namespaceName}.{name}";
            return new Component(id, name, namespaceName, null, filePath, kind, ResolveVisibility(modifiers));
        }

        private void AddObservation(TypeSyntax type, DependencyKind kind, UsageContext usage)
        {
            if (!_componentStack.TryPeek(out Component? source))
            {
                return;
            }

            string targetName = ExtractTypeName(type);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            FileLinePositionSpan span = tree.GetLineSpan(type.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                kind,
                usage,
                filePath,
                span.StartLinePosition.Line + 1,
                type.ToString()));
        }

        private static string ExtractTypeName(TypeSyntax type)
        {
            return type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                NullableTypeSyntax nullable => ExtractTypeName(nullable.ElementType),
                ArrayTypeSyntax array => ExtractTypeName(array.ElementType),
                PredefinedTypeSyntax => "",
                _ => type.ToString().Split('.').Last().Split('<').First(),
            };
        }

        private static Visibility ResolveVisibility(SyntaxTokenList modifiers)
        {
            bool hasPublic = modifiers.Any(SyntaxKind.PublicKeyword);
            bool hasPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);
            bool hasProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
            bool hasInternal = modifiers.Any(SyntaxKind.InternalKeyword);

            return (hasPublic, hasPrivate, hasProtected, hasInternal) switch
            {
                (true, _, _, _) => Visibility.Public,
                (_, true, true, _) => Visibility.PrivateProtected,
                (_, _, true, true) => Visibility.ProtectedInternal,
                (_, _, true, _) => Visibility.Protected,
                (_, true, _, _) => Visibility.Private,
                (_, _, _, true) => Visibility.Internal,
                _ => Visibility.Internal,
            };
        }
    }

    private sealed record UsingNamespace(string Name, int Line);
}
