using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Cli.Analysis;

public sealed class CSharpDependencyAnalyzer
{
    private static readonly string[] ExcludedDirectoryNames = [".git", ".vs", "bin", "obj"];

    public AnalysisReport Analyze(string targetPath, bool useGit, int gitMonths)
    {
        string fullPath = Path.GetFullPath(targetPath);
        IReadOnlyList<string> files = DiscoverCSharpFiles(fullPath);
        List<Component> components = [];
        List<DependencyObservation> observations = [];

        foreach (string file in files)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            string namespaceName = FindNamespace(root);
            ComponentWalker walker = new(tree, namespaceName, file);
            walker.Visit(root);
            components.AddRange(walker.Components);
            observations.AddRange(walker.Observations);
        }

        Dictionary<string, Component> componentsByName = components
            .GroupBy(component => component.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        IReadOnlyDictionary<string, int> changeCounts = useGit
            ? GitVolatility.GetChangeCounts(fullPath, gitMonths)
            : new Dictionary<string, int>(StringComparer.Ordinal);

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

            Component? source = components.FirstOrDefault(component => component.Id == observation.SourceComponentId);
            if (source is null)
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

        GradeResult grade = new("B", "Bootstrap", "issue-density", "Phase 0 provides analysis plumbing; issue detection is not enabled yet.");
        AnalysisSummary summary = new(
            fullPath,
            "syntax-only",
            files.Count,
            components.Count,
            couplings.Count,
            0,
            useGit && changeCounts.Count > 0,
            gitMonths);

        return new AnalysisReport(
            summary,
            grade,
            couplings.Count == 0 ? 1.0 : 0.75,
            components,
            observations,
            couplings,
            [],
            [
                "Semantic symbol resolution is not enabled.",
                "DI container runtime resolution is not analyzed in syntax-only mode.",
                "Reflection and dynamic calls may be incomplete.",
                "Generated code is excluded by default.",
            ]);
    }

    private static IReadOnlyList<string> DiscoverCSharpFiles(string fullPath)
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
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
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
}
