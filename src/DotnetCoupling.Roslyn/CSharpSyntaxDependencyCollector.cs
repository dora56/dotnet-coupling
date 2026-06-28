using DotnetCoupling.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Roslyn;

internal static class CSharpSyntaxDependencyCollector
{
    internal static SyntaxFileAnalysis AnalyzeFile(string filePath, string? projectName = null)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        string namespaceName = FindNamespace(root);
        ComponentWalker walker = new(tree, namespaceName, filePath, projectName);
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

    private sealed class ComponentWalker(SyntaxTree tree, string namespaceName, string filePath, string? projectName) : CSharpSyntaxWalker
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
                QualifiedNameSyntax qualified => ExtractSimpleName(qualified.Right),
                AliasQualifiedNameSyntax aliasQualified => ExtractSimpleName(aliasQualified.Name),
                GenericNameSyntax generic => CreateTypeIdentity(generic.Identifier.ValueText, generic.TypeArgumentList.Arguments.Count),
                NullableTypeSyntax nullable => ExtractTypeName(nullable.ElementType),
                ArrayTypeSyntax array => ExtractTypeName(array.ElementType),
                PredefinedTypeSyntax => "",
                _ => type.ToString().Split('.').Last().Split('<').First(),
            };
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
