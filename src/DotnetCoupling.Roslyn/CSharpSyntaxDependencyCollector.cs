using DotnetCoupling.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Roslyn;

internal static class CSharpSyntaxDependencyCollector
{
    internal static SyntaxFileAnalysis AnalyzeFile(string filePath, string? projectName = null)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        return AnalyzeRoot(tree, root, filePath, projectName, semanticModel: null);
    }

    internal static SyntaxFileAnalysis AnalyzeDocument(Document document, string? projectName = null)
    {
        SyntaxTree tree = document.GetSyntaxTreeAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"Document has no syntax tree: {document.FilePath ?? document.Name}");
        CompilationUnitSyntax root = document.GetSyntaxRootAsync().GetAwaiter().GetResult() as CompilationUnitSyntax
            ?? throw new InvalidOperationException($"Document root is not a compilation unit: {document.FilePath ?? document.Name}");
        SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"Document has no semantic model: {document.FilePath ?? document.Name}");

        return AnalyzeRoot(tree, root, document.FilePath ?? tree.FilePath, projectName, semanticModel);
    }

    private static SyntaxFileAnalysis AnalyzeRoot(
        SyntaxTree tree,
        CompilationUnitSyntax root,
        string filePath,
        string? projectName,
        SemanticModel? semanticModel)
    {
        string namespaceName = FindNamespace(root);
        ComponentWalker walker = new(tree, namespaceName, filePath, projectName, semanticModel);
        walker.Visit(root);

        return new SyntaxFileAnalysis(
            walker.Components,
            walker.Observations,
            CollectUsingNamespaces(tree, root));
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
        foreach (UsingDirectiveSyntax usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
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

    private sealed class ComponentWalker(
        SyntaxTree tree,
        string namespaceName,
        string filePath,
        string? projectName,
        SemanticModel? semanticModel) : CSharpSyntaxWalker
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
            Component component = CreateComponent(node.Identifier.ValueText, arity: 0, ComponentKind.Enum, node.Modifiers);
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

        public override void VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node)
        {
            foreach (TypeConstraintSyntax constraint in node.Constraints.OfType<TypeConstraintSyntax>())
            {
                AddObservation(constraint.Type, DependencyKind.GenericConstraint, UsageContext.GenericConstraint);
            }

            base.VisitTypeParameterConstraintClause(node);
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

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (node.Parent is LocalDeclarationStatementSyntax or ForStatementSyntax or UsingStatementSyntax)
            {
                if (semanticModel is null
                    && node.Type is IdentifierNameSyntax identifierName
                    && string.Equals(identifierName.Identifier.ValueText, "var", StringComparison.Ordinal))
                {
                    base.VisitVariableDeclaration(node);
                    return;
                }

                AddObservation(node.Type, DependencyKind.TypeReference, UsageContext.LocalVariableType);
            }

            base.VisitVariableDeclaration(node);
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

        public override void VisitAttribute(AttributeSyntax node)
        {
            AddAttributeObservation(node);
            base.VisitAttribute(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            AddInvocationObservation(node);
            base.VisitInvocationExpression(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            AddMemberAccessObservation(node);
            base.VisitMemberAccessExpression(node);
        }

        private void VisitTypeDeclaration(TypeDeclarationSyntax node, ComponentKind kind, Action visitChildren)
        {
            Component component = CreateComponent(
                node.Identifier.ValueText,
                node.TypeParameterList?.Parameters.Count ?? 0,
                kind,
                node.Modifiers);
            Components.Add(component);
            _componentStack.Push(component);
            visitChildren();
            _componentStack.Pop();
        }

        private Component CreateComponent(string name, int arity, ComponentKind kind, SyntaxTokenList modifiers)
        {
            string typeName = CreateTypeIdentity(name, arity);
            string id = string.IsNullOrWhiteSpace(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
            return new Component(id, name, namespaceName, projectName, filePath, kind, ResolveVisibility(modifiers));
        }

        private void AddObservation(TypeSyntax type, DependencyKind kind, UsageContext usage)
        {
            if (!_componentStack.TryPeek(out Component? source))
            {
                return;
            }

            string targetName = ExtractTypeName(type, semanticModel);
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

        private void AddInvocationObservation(InvocationExpressionSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source) || semanticModel is null)
            {
                return;
            }

            IMethodSymbol? methodSymbol = (semanticModel.GetOperation(node) as IInvocationOperation)?.TargetMethod;
            if (methodSymbol is null)
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node.Expression);
                ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol is null)
                {
                    return;
                }
            }

            string? targetName = TryCreateSymbolIdentity(methodSymbol.ContainingType);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.Expression.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                methodSymbol.IsStatic ? DependencyKind.StaticCall : DependencyKind.MethodCall,
                methodSymbol.IsStatic ? UsageContext.StaticCall : UsageContext.MethodCall,
                filePath,
                span.StartLinePosition.Line + 1,
                node.Expression.ToString()));
        }

        private void AddAttributeObservation(AttributeSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source) || semanticModel is null)
            {
                return;
            }

            ISymbol? symbol = semanticModel.GetSymbolInfo(node).Symbol;
            IMethodSymbol? methodSymbol = symbol as IMethodSymbol;
            string? targetName = methodSymbol?.ContainingType is null
                ? null
                : TryCreateSymbolIdentity(methodSymbol.ContainingType);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                DependencyKind.Attribute,
                UsageContext.Attribute,
                filePath,
                span.StartLinePosition.Line + 1,
                node.Name.ToString()));
        }

        private void AddMemberAccessObservation(MemberAccessExpressionSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source) || semanticModel is null)
            {
                return;
            }

            if (node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node)
            {
                return;
            }

            IOperation? operation = semanticModel.GetOperation(node);
            ISymbol? memberSymbol = operation switch
            {
                IPropertyReferenceOperation propertyReference => propertyReference.Property,
                IFieldReferenceOperation fieldReference => fieldReference.Field,
                _ => null,
            };

            if (memberSymbol is null)
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);
                memberSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            }

            if (memberSymbol is not IPropertySymbol and not IFieldSymbol)
            {
                return;
            }

            string? targetName = memberSymbol.ContainingType is null
                ? null
                : TryCreateSymbolIdentity(memberSymbol.ContainingType);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            (DependencyKind kind, UsageContext usage) = memberSymbol switch
            {
                IPropertySymbol => (DependencyKind.PropertyAccess, UsageContext.PropertyAccess),
                IFieldSymbol => (DependencyKind.FieldAccess, UsageContext.FieldAccess),
                _ => throw new InvalidOperationException("Unsupported member access symbol."),
            };

            FileLinePositionSpan span = tree.GetLineSpan(node.Name.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                kind,
                usage,
                filePath,
                span.StartLinePosition.Line + 1,
                node.ToString()));
        }

        private static string ExtractTypeName(TypeSyntax type, SemanticModel? semanticModel)
        {
            string? resolvedName = TryResolveTypeName(type, semanticModel);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                return resolvedName;
            }

            return type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                QualifiedNameSyntax qualified => ExtractSimpleName(qualified.Right),
                AliasQualifiedNameSyntax aliasQualified => ExtractSimpleName(aliasQualified.Name),
                GenericNameSyntax generic => CreateTypeIdentity(generic.Identifier.ValueText, generic.TypeArgumentList.Arguments.Count),
                NullableTypeSyntax nullable => ExtractTypeName(nullable.ElementType, semanticModel),
                ArrayTypeSyntax array => ExtractTypeName(array.ElementType, semanticModel),
                PredefinedTypeSyntax => "",
                _ => type.ToString().Split('.').Last().Split('<').First(),
            };
        }

        private static string? TryResolveTypeName(TypeSyntax type, SemanticModel? semanticModel)
        {
            if (semanticModel is null)
            {
                return null;
            }

            ITypeSymbol? typeSymbol = semanticModel.GetTypeInfo(type).Type;
            if (typeSymbol is null)
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(type);
                ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (symbol is IAliasSymbol aliasSymbol)
                {
                    symbol = aliasSymbol.Target;
                }

                typeSymbol = symbol switch
                {
                    ITypeSymbol directType => directType,
                    IMethodSymbol methodSymbol => methodSymbol.ReturnType,
                    IPropertySymbol propertySymbol => propertySymbol.Type,
                    IFieldSymbol fieldSymbol => fieldSymbol.Type,
                    _ => null,
                };
            }

            return typeSymbol switch
            {
                IArrayTypeSymbol arrayType => TryCreateSymbolIdentity(arrayType.ElementType),
                INamedTypeSymbol namedType => TryCreateSymbolIdentity(namedType),
                _ => null,
            };
        }

        private static string? TryCreateSymbolIdentity(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                return TryCreateSymbolIdentity(namedType);
            }

            return null;
        }

        private static string? TryCreateSymbolIdentity(INamedTypeSymbol namedType)
        {
            if (namedType.SpecialType != SpecialType.None)
            {
                return null;
            }

            if (namedType.ContainingNamespace is null || namedType.ContainingNamespace.IsGlobalNamespace)
            {
                return namedType.MetadataName;
            }

            string namespacePrefix = namedType.ContainingNamespace.ToDisplayString();
            return $"{namespacePrefix}.{namedType.MetadataName}";
        }

        private static string ExtractSimpleName(SimpleNameSyntax name)
        {
            return name switch
            {
                GenericNameSyntax generic => CreateTypeIdentity(generic.Identifier.ValueText, generic.TypeArgumentList.Arguments.Count),
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => name.Identifier.ValueText,
            };
        }

        private static string CreateTypeIdentity(string name, int arity)
        {
            return arity == 0 ? name : $"{name}`{arity}";
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

internal sealed record SyntaxFileAnalysis(
    IReadOnlyList<Component> Components,
    IReadOnlyList<DependencyObservation> Observations,
    IReadOnlyList<UsingNamespace> UsingNamespaces);
