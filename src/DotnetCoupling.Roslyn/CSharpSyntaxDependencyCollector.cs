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
        private readonly Dictionary<string, string> _dynamicLocalTargets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _dynamicFieldTargets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _dynamicPropertyTargets = new(StringComparer.Ordinal);

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
                AddBaseTypeObservation(baseType.Type);
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
            RegisterDynamicFieldTargets(node);
            AddObservation(node.Declaration.Type, DependencyKind.TypeReference, UsageContext.FieldType);
            base.VisitFieldDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            RegisterDynamicPropertyTarget(node);
            AddObservation(node.Type, DependencyKind.TypeReference, UsageContext.PropertyType);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (node.Parent is LocalDeclarationStatementSyntax or ForStatementSyntax or UsingStatementSyntax)
            {
                RegisterDynamicLocalTargets(node);
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

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            AddObservation(node.Type, DependencyKind.Reflection, UsageContext.Reflection);
            base.VisitTypeOfExpression(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            AddAttributeObservation(node);
            base.VisitAttribute(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            AddActivatorReflectionObservation(node);
            if (AddServiceLocatorObservation(node))
            {
                base.VisitInvocationExpression(node);
                return;
            }

            if (AddDynamicDispatchObservation(node))
            {
                base.VisitInvocationExpression(node);
                return;
            }

            AddNameOfObservation(node);
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

        private void AddBaseTypeObservation(TypeSyntax type)
        {
            if (semanticModel is not null)
            {
                ITypeSymbol? typeSymbol = semanticModel.GetTypeInfo(type).Type;
                if (typeSymbol?.TypeKind == TypeKind.Interface)
                {
                    AddObservation(type, DependencyKind.InterfaceImplementation, UsageContext.InterfaceImplementation);
                    return;
                }
            }

            AddObservation(type, DependencyKind.Inheritance, UsageContext.BaseType);
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

        private void AddNameOfObservation(InvocationExpressionSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source)
                || node.Expression is not IdentifierNameSyntax identifierName
                || !string.Equals(identifierName.Identifier.ValueText, "nameof", StringComparison.Ordinal)
                || node.ArgumentList.Arguments.Count != 1)
            {
                return;
            }

            string? targetName = ExtractNameOfTargetName(node.ArgumentList.Arguments[0].Expression);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.ArgumentList.Arguments[0].Expression.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                DependencyKind.Reflection,
                UsageContext.Reflection,
                filePath,
                span.StartLinePosition.Line + 1,
                node.ToString()));
        }

        private bool AddServiceLocatorObservation(InvocationExpressionSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source))
            {
                return false;
            }

            string? targetName = TryResolveServiceLocatorTargetName(node);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return false;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.Expression.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                DependencyKind.Dynamic,
                UsageContext.ServiceLocator,
                filePath,
                span.StartLinePosition.Line + 1,
                node.Expression.ToString()));
            return true;
        }

        private void AddActivatorReflectionObservation(InvocationExpressionSyntax node)
        {
            if (!_componentStack.TryPeek(out Component? source))
            {
                return;
            }

            string? targetName = TryResolveActivatorReflectionTargetName(node);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.Expression.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                DependencyKind.Reflection,
                UsageContext.Reflection,
                filePath,
                span.StartLinePosition.Line + 1,
                node.Expression.ToString()));
        }

        private void RegisterDynamicLocalTargets(VariableDeclarationSyntax node)
        {
            if (!node.Type.IsKind(SyntaxKind.IdentifierName)
                || node.Type is not IdentifierNameSyntax identifierName
                || !string.Equals(identifierName.Identifier.ValueText, "dynamic", StringComparison.Ordinal))
            {
                return;
            }

            foreach (VariableDeclaratorSyntax variable in node.Variables)
            {
                if (variable.Initializer?.Value is null)
                {
                    continue;
                }

                string? targetName = variable.Initializer.Value switch
                {
                    ObjectCreationExpressionSyntax objectCreation => ExtractTypeName(objectCreation.Type, semanticModel),
                    _ => ExtractExpressionTargetName(variable.Initializer.Value),
                };

                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    _dynamicLocalTargets[variable.Identifier.ValueText] = targetName;
                }
            }
        }

        private void RegisterDynamicFieldTargets(FieldDeclarationSyntax node)
        {
            if (!node.Declaration.Type.IsKind(SyntaxKind.IdentifierName)
                || node.Declaration.Type is not IdentifierNameSyntax identifierName
                || !string.Equals(identifierName.Identifier.ValueText, "dynamic", StringComparison.Ordinal))
            {
                return;
            }

            foreach (VariableDeclaratorSyntax variable in node.Declaration.Variables)
            {
                if (variable.Initializer?.Value is null)
                {
                    continue;
                }

                string? targetName = variable.Initializer.Value switch
                {
                    ObjectCreationExpressionSyntax objectCreation => ExtractTypeName(objectCreation.Type, semanticModel),
                    _ => ExtractExpressionTargetName(variable.Initializer.Value),
                };

                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    _dynamicFieldTargets[variable.Identifier.ValueText] = targetName;
                }
            }
        }

        private void RegisterDynamicPropertyTarget(PropertyDeclarationSyntax node)
        {
            if (!node.Type.IsKind(SyntaxKind.IdentifierName)
                || node.Type is not IdentifierNameSyntax identifierName
                || !string.Equals(identifierName.Identifier.ValueText, "dynamic", StringComparison.Ordinal)
                || node.Initializer?.Value is null)
            {
                return;
            }

            string? targetName = node.Initializer.Value switch
            {
                ObjectCreationExpressionSyntax objectCreation => ExtractTypeName(objectCreation.Type, semanticModel),
                _ => ExtractExpressionTargetName(node.Initializer.Value),
            };

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                _dynamicPropertyTargets[node.Identifier.ValueText] = targetName;
            }
        }

        private bool AddDynamicDispatchObservation(InvocationExpressionSyntax node)
        {
            ExpressionSyntax receiverExpression = TryGetDynamicDispatchReceiverExpression(node, out ExpressionSyntax? resolvedReceiverExpression)
                ? resolvedReceiverExpression
                : node.Expression;
            bool hasDynamicDispatchShape = node.Expression is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax;
            if (_componentStack.TryPeek(out Component? sourceFromDynamicLocal)
                && hasDynamicDispatchShape
                && TryGetDynamicLocalTargetName(receiverExpression, out string? dynamicLocalTargetName)
                && !string.IsNullOrWhiteSpace(dynamicLocalTargetName))
            {
                FileLinePositionSpan dynamicLocalSpan = tree.GetLineSpan(node.Expression.Span);
                Observations.Add(new DependencyObservation(
                    sourceFromDynamicLocal.Id,
                    dynamicLocalTargetName,
                    DependencyKind.Dynamic,
                    UsageContext.DynamicDispatch,
                    filePath,
                    dynamicLocalSpan.StartLinePosition.Line + 1,
                    node.Expression.ToString()));
                return true;
            }

            if (_componentStack.TryPeek(out Component? sourceFromDynamicField)
                && hasDynamicDispatchShape
                && TryGetDynamicFieldTargetName(receiverExpression, out string? dynamicFieldTargetName)
                && !string.IsNullOrWhiteSpace(dynamicFieldTargetName))
            {
                FileLinePositionSpan dynamicFieldSpan = tree.GetLineSpan(node.Expression.Span);
                Observations.Add(new DependencyObservation(
                    sourceFromDynamicField.Id,
                    dynamicFieldTargetName,
                    DependencyKind.Dynamic,
                    UsageContext.DynamicDispatch,
                    filePath,
                    dynamicFieldSpan.StartLinePosition.Line + 1,
                    node.Expression.ToString()));
                return true;
            }

            if (_componentStack.TryPeek(out Component? sourceFromDynamicProperty)
                && hasDynamicDispatchShape
                && TryGetDynamicPropertyTargetName(receiverExpression, out string? dynamicPropertyTargetName)
                && !string.IsNullOrWhiteSpace(dynamicPropertyTargetName))
            {
                FileLinePositionSpan dynamicPropertySpan = tree.GetLineSpan(node.Expression.Span);
                Observations.Add(new DependencyObservation(
                    sourceFromDynamicProperty.Id,
                    dynamicPropertyTargetName,
                    DependencyKind.Dynamic,
                    UsageContext.DynamicDispatch,
                    filePath,
                    dynamicPropertySpan.StartLinePosition.Line + 1,
                    node.Expression.ToString()));
                return true;
            }

            if (!_componentStack.TryPeek(out Component? source)
                || !hasDynamicDispatchShape
                || receiverExpression is not CastExpressionSyntax castExpression
                || !castExpression.Type.IsKind(SyntaxKind.IdentifierName)
                || castExpression.Type is not IdentifierNameSyntax identifierName
                || !string.Equals(identifierName.Identifier.ValueText, "dynamic", StringComparison.Ordinal))
            {
                return false;
            }

            string? targetName = castExpression.Expression switch
            {
                ObjectCreationExpressionSyntax objectCreation => ExtractTypeName(objectCreation.Type, semanticModel),
                _ => ExtractExpressionTargetName(castExpression.Expression),
            };
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return false;
            }

            FileLinePositionSpan span = tree.GetLineSpan(node.Expression.Span);
            Observations.Add(new DependencyObservation(
                source.Id,
                targetName,
                DependencyKind.Dynamic,
                UsageContext.DynamicDispatch,
                filePath,
                span.StartLinePosition.Line + 1,
                node.Expression.ToString()));
            return true;
        }

        private static bool TryGetDynamicDispatchReceiverExpression(InvocationExpressionSyntax node, out ExpressionSyntax receiverExpression)
        {
            receiverExpression = node.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => UnwrapParentheses(memberAccess.Expression),
                MemberBindingExpressionSyntax when node.Parent is ConditionalAccessExpressionSyntax conditionalAccess
                    => UnwrapParentheses(conditionalAccess.Expression),
                _ => node.Expression,
            };

            return node.Expression is MemberAccessExpressionSyntax
                || node.Expression is MemberBindingExpressionSyntax && node.Parent is ConditionalAccessExpressionSyntax;
        }

        private bool TryGetDynamicLocalTargetName(ExpressionSyntax receiverExpression, out string? targetName)
        {
            receiverExpression = UnwrapParentheses(receiverExpression);
            if (receiverExpression is IdentifierNameSyntax identifier
                && _dynamicLocalTargets.TryGetValue(identifier.Identifier.ValueText, out string? resolvedTargetName))
            {
                targetName = resolvedTargetName;
                return true;
            }

            targetName = null;
            return false;
        }

        private bool TryGetDynamicFieldTargetName(ExpressionSyntax receiverExpression, out string? targetName)
        {
            receiverExpression = UnwrapParentheses(receiverExpression);
            if (receiverExpression is IdentifierNameSyntax identifier
                && _dynamicFieldTargets.TryGetValue(identifier.Identifier.ValueText, out string? resolvedTargetName))
            {
                targetName = resolvedTargetName;
                return true;
            }

            targetName = null;
            return false;
        }

        private bool TryGetDynamicPropertyTargetName(ExpressionSyntax receiverExpression, out string? targetName)
        {
            receiverExpression = UnwrapParentheses(receiverExpression);
            if (receiverExpression is IdentifierNameSyntax identifier
                && _dynamicPropertyTargets.TryGetValue(identifier.Identifier.ValueText, out string? resolvedTargetName))
            {
                targetName = resolvedTargetName;
                return true;
            }

            targetName = null;
            return false;
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

        private string? ExtractNameOfTargetName(ExpressionSyntax expression)
        {
            string? resolvedName = TryResolveNameOfTargetName(expression);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                return resolvedName;
            }

            return expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                GenericNameSyntax generic => CreateTypeIdentity(generic.Identifier.ValueText, generic.TypeArgumentList.Arguments.Count),
                QualifiedNameSyntax qualified => ExtractSimpleName(qualified.Right),
                AliasQualifiedNameSyntax aliasQualified => ExtractSimpleName(aliasQualified.Name),
                MemberAccessExpressionSyntax memberAccess => ExtractNameOfTargetName(memberAccess.Expression),
                MemberBindingExpressionSyntax => null,
                _ => null,
            };
        }

        private string? TryResolveNameOfTargetName(ExpressionSyntax expression)
        {
            if (semanticModel is null)
            {
                return null;
            }

            IOperation? operation = semanticModel.GetOperation(expression);
            ISymbol? symbol = operation switch
            {
                ITypeOfOperation typeOfOperation => typeOfOperation.TypeOperand,
                IMemberReferenceOperation memberReferenceOperation => memberReferenceOperation.Member,
                ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local,
                IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter,
                _ => null,
            };

            if (symbol is null)
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression);
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            }

            if (symbol is IAliasSymbol aliasSymbol)
            {
                symbol = aliasSymbol.Target;
            }

            return symbol switch
            {
                INamedTypeSymbol namedType => TryCreateSymbolIdentity(namedType),
                IMethodSymbol methodSymbol when methodSymbol.ContainingType is not null => TryCreateSymbolIdentity(methodSymbol.ContainingType),
                IPropertySymbol propertySymbol when propertySymbol.ContainingType is not null => TryCreateSymbolIdentity(propertySymbol.ContainingType),
                IFieldSymbol fieldSymbol when fieldSymbol.ContainingType is not null => TryCreateSymbolIdentity(fieldSymbol.ContainingType),
                ILocalSymbol localSymbol => TryCreateSymbolIdentity(localSymbol.Type),
                IParameterSymbol parameterSymbol => TryCreateSymbolIdentity(parameterSymbol.Type),
                _ => null,
            };
        }

        private string? ExtractExpressionTargetName(ExpressionSyntax expression)
        {
            expression = UnwrapParentheses(expression);
            string? resolvedName = TryResolveExpressionTargetName(expression);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                return resolvedName;
            }

            return expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                GenericNameSyntax generic => CreateTypeIdentity(generic.Identifier.ValueText, generic.TypeArgumentList.Arguments.Count),
                QualifiedNameSyntax qualified => ExtractSimpleName(qualified.Right),
                AliasQualifiedNameSyntax aliasQualified => ExtractSimpleName(aliasQualified.Name),
                MemberAccessExpressionSyntax memberAccess => ExtractExpressionTargetName(memberAccess.Expression),
                _ => null,
            };
        }

        private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
            }

            return expression;
        }

        private string? TryResolveExpressionTargetName(ExpressionSyntax expression)
        {
            expression = UnwrapParentheses(expression);
            if (semanticModel is null)
            {
                return null;
            }

            IOperation? operation = semanticModel.GetOperation(expression);
            ITypeSymbol? typeSymbol = operation switch
            {
                IObjectCreationOperation objectCreationOperation => objectCreationOperation.Type,
                IConversionOperation conversionOperation => conversionOperation.Operand.Type,
                ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local.Type,
                IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter.Type,
                IPropertyReferenceOperation propertyReferenceOperation => propertyReferenceOperation.Property.Type,
                IFieldReferenceOperation fieldReferenceOperation => fieldReferenceOperation.Field.Type,
                _ => operation?.Type,
            };

            return typeSymbol is null ? null : TryCreateSymbolIdentity(typeSymbol);
        }

        private string? TryResolveServiceLocatorTargetName(InvocationExpressionSyntax node)
        {
            if (semanticModel is not null)
            {
                IMethodSymbol? methodSymbol = (semanticModel.GetOperation(node) as IInvocationOperation)?.TargetMethod;
                if (methodSymbol is null)
                {
                    SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node.Expression);
                    methodSymbol = symbolInfo.Symbol as IMethodSymbol
                        ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                }

                if (methodSymbol is not null && IsServiceLocatorMethod(methodSymbol))
                {
                    int typeOfArgumentIndex = TryGetServiceLocatorTypeOfArgumentIndex(node, methodSymbol.Name);
                    if (typeOfArgumentIndex >= 0
                        && node.ArgumentList.Arguments[typeOfArgumentIndex].Expression is TypeOfExpressionSyntax semanticTypeOfExpression)
                    {
                        return TryResolveTypeName(semanticTypeOfExpression.Type, semanticModel);
                    }

                    if (methodSymbol.TypeArguments.Length == 1)
                    {
                        return TryCreateSymbolIdentity(methodSymbol.TypeArguments[0]);
                    }

                    return TryCreateSymbolIdentity(methodSymbol.ReturnType);
                }

                if (methodSymbol is not null && IsActivatorUtilitiesCreateInstanceMethod(methodSymbol))
                {
                    if (methodSymbol.TypeArguments.Length == 1)
                    {
                        return TryCreateSymbolIdentity(methodSymbol.TypeArguments[0]);
                    }

                    if (node.ArgumentList.Arguments.Count >= 2
                        && node.ArgumentList.Arguments[1].Expression is TypeOfExpressionSyntax semanticTypeOfExpression)
                    {
                        return TryResolveTypeName(semanticTypeOfExpression.Type, semanticModel);
                    }
                }
            }

            if ((node.Expression is MemberAccessExpressionSyntax serviceLocatorMemberAccess
                    && IsServiceLocatorMethodName(serviceLocatorMemberAccess.Name.Identifier.ValueText)
                || node.Expression is MemberBindingExpressionSyntax serviceLocatorMemberBinding
                    && IsServiceLocatorMethodName(serviceLocatorMemberBinding.Name.Identifier.ValueText)))
            {
                string? methodName = node.Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                    MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
                    _ => null,
                };

                int typeOfArgumentIndex = TryGetServiceLocatorTypeOfArgumentIndex(node, methodName);
                if (typeOfArgumentIndex >= 0
                    && node.ArgumentList.Arguments[typeOfArgumentIndex].Expression is TypeOfExpressionSyntax typeOfExpression)
                {
                    return ExtractTypeName(typeOfExpression.Type, semanticModel);
                }
            }

            if (node.Expression is MemberAccessExpressionSyntax activatorUtilitiesMemberAccess
                && IsActivatorUtilitiesReceiver(activatorUtilitiesMemberAccess.Expression)
                && activatorUtilitiesMemberAccess.Name is GenericNameSyntax activatorUtilitiesGenericName
                && string.Equals(activatorUtilitiesGenericName.Identifier.ValueText, "CreateInstance", StringComparison.Ordinal)
                && activatorUtilitiesGenericName.TypeArgumentList.Arguments.Count == 1)
            {
                return ExtractTypeName(activatorUtilitiesGenericName.TypeArgumentList.Arguments[0], semanticModel);
            }

            if (node.Expression is MemberAccessExpressionSyntax nonGenericActivatorUtilitiesMemberAccess
                && IsActivatorUtilitiesReceiver(nonGenericActivatorUtilitiesMemberAccess.Expression)
                && string.Equals(nonGenericActivatorUtilitiesMemberAccess.Name.Identifier.ValueText, "CreateInstance", StringComparison.Ordinal)
                && node.ArgumentList.Arguments.Count >= 2
                && node.ArgumentList.Arguments[1].Expression is TypeOfExpressionSyntax activatorUtilitiesTypeOfExpression)
            {
                return ExtractTypeName(activatorUtilitiesTypeOfExpression.Type, semanticModel);
            }

            GenericNameSyntax? genericName = node.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name as GenericNameSyntax,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name as GenericNameSyntax,
                GenericNameSyntax directGenericName => directGenericName,
                _ => null,
            };

            if (!IsServiceLocatorMethodName(genericName?.Identifier.ValueText)
                || genericName?.TypeArgumentList.Arguments.Count != 1)
            {
                return null;
            }

            return ExtractTypeName(genericName.TypeArgumentList.Arguments[0], semanticModel);
        }

        private string? TryResolveActivatorReflectionTargetName(InvocationExpressionSyntax node)
        {
            if (semanticModel is not null)
            {
                IMethodSymbol? methodSymbol = (semanticModel.GetOperation(node) as IInvocationOperation)?.TargetMethod;
                if (methodSymbol is null)
                {
                    SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node.Expression);
                    methodSymbol = symbolInfo.Symbol as IMethodSymbol
                        ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                }

                if (methodSymbol is not null
                    && string.Equals(methodSymbol.Name, "CreateInstance", StringComparison.Ordinal)
                    && string.Equals(methodSymbol.ContainingType.ToDisplayString(), "System.Activator", StringComparison.Ordinal))
                {
                    if (methodSymbol.TypeArguments.Length == 1)
                    {
                        return TryCreateSymbolIdentity(methodSymbol.TypeArguments[0]);
                    }

                    if (node.ArgumentList.Arguments.Count == 1
                        && node.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax semanticTypeOfExpression)
                    {
                        return TryResolveTypeName(semanticTypeOfExpression.Type, semanticModel);
                    }
                }
            }

            if (!IsActivatorCreateInstanceExpression(node.Expression, out GenericNameSyntax? genericName))
            {
                return null;
            }

            if (genericName is not null && genericName.TypeArgumentList.Arguments.Count == 1)
            {
                return ExtractTypeName(genericName.TypeArgumentList.Arguments[0], semanticModel);
            }

            if (node.ArgumentList.Arguments.Count == 1
                && node.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax typeOfExpression)
            {
                return ExtractTypeName(typeOfExpression.Type, semanticModel);
            }

            return null;
        }

        private static bool IsActivatorCreateInstanceExpression(ExpressionSyntax expression, out GenericNameSyntax? genericName)
        {
            genericName = null;

            return expression switch
            {
                MemberAccessExpressionSyntax memberAccess
                    when IsActivatorReceiver(memberAccess.Expression)
                    && TryGetCreateInstanceGenericName(memberAccess.Name, out genericName) => true,
                _ => false,
            };
        }

        private static bool TryGetCreateInstanceGenericName(SimpleNameSyntax name, out GenericNameSyntax? genericName)
        {
            if (name is GenericNameSyntax generic
                && string.Equals(generic.Identifier.ValueText, "CreateInstance", StringComparison.Ordinal))
            {
                genericName = generic;
                return true;
            }

            genericName = null;
            return name is IdentifierNameSyntax identifier
                && string.Equals(identifier.Identifier.ValueText, "CreateInstance", StringComparison.Ordinal);
        }

        private static bool IsActivatorReceiver(ExpressionSyntax expression)
        {
            expression = UnwrapParentheses(expression);
            return expression switch
            {
                IdentifierNameSyntax identifier => string.Equals(identifier.Identifier.ValueText, "Activator", StringComparison.Ordinal),
                QualifiedNameSyntax qualified => string.Equals(qualified.Right.Identifier.ValueText, "Activator", StringComparison.Ordinal),
                AliasQualifiedNameSyntax aliasQualified => string.Equals(aliasQualified.Name.Identifier.ValueText, "Activator", StringComparison.Ordinal),
                MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, "Activator", StringComparison.Ordinal),
                _ => false,
            };
        }

        private static bool IsServiceLocatorMethod(IMethodSymbol? methodSymbol)
        {
            return methodSymbol is not null && IsServiceLocatorMethodName(methodSymbol.Name);
        }

        private static bool IsServiceLocatorMethodName(string? methodName)
        {
            return string.Equals(methodName, "GetService", StringComparison.Ordinal)
                || string.Equals(methodName, "GetRequiredService", StringComparison.Ordinal)
                || string.Equals(methodName, "GetKeyedService", StringComparison.Ordinal)
                || string.Equals(methodName, "GetRequiredKeyedService", StringComparison.Ordinal);
        }

        private static int TryGetServiceLocatorTypeOfArgumentIndex(InvocationExpressionSyntax node, string? methodName)
        {
            if (string.Equals(methodName, "GetService", StringComparison.Ordinal)
                || string.Equals(methodName, "GetRequiredService", StringComparison.Ordinal))
            {
                return node.ArgumentList.Arguments.Count == 1 ? 0 : -1;
            }

            if (string.Equals(methodName, "GetKeyedService", StringComparison.Ordinal)
                || string.Equals(methodName, "GetRequiredKeyedService", StringComparison.Ordinal))
            {
                return node.ArgumentList.Arguments.Count >= 1 ? 0 : -1;
            }

            return -1;
        }

        private static bool IsActivatorUtilitiesCreateInstanceMethod(IMethodSymbol? methodSymbol)
        {
            return methodSymbol is not null
                && string.Equals(methodSymbol.Name, "CreateInstance", StringComparison.Ordinal)
                && string.Equals(
                    methodSymbol.ContainingType.ToDisplayString(),
                    "Microsoft.Extensions.DependencyInjection.ActivatorUtilities",
                    StringComparison.Ordinal);
        }

        private static bool IsActivatorUtilitiesReceiver(ExpressionSyntax expression)
        {
            expression = UnwrapParentheses(expression);
            return expression switch
            {
                IdentifierNameSyntax identifier => string.Equals(identifier.Identifier.ValueText, "ActivatorUtilities", StringComparison.Ordinal),
                MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, "ActivatorUtilities", StringComparison.Ordinal),
                _ => false,
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
