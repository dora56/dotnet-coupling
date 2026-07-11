using DotnetCoupling.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Roslyn.Collection;

internal static class CSharpComplexityCollector
{
    internal static IReadOnlyList<MemberComplexity> Collect(
        SyntaxTree tree,
        CompilationUnitSyntax root,
        string namespaceName,
        string filePath,
        SemanticModel? semanticModel = null)
    {
        ComplexityCollectorWalker walker = new(tree, namespaceName, filePath, semanticModel);
        walker.Visit(root);
        return walker.Members;
    }

    private sealed class ComplexityCollectorWalker(
        SyntaxTree tree,
        string namespaceName,
        string filePath,
        SemanticModel? semanticModel) : CSharpSyntaxWalker
    {
        private readonly Stack<string> _componentIds = new();

        public List<MemberComplexity> Members { get; } = [];

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitClassDeclaration(node));
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitRecordDeclaration(node));
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitStructDeclaration(node));
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitInterfaceDeclaration(node));
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AddMember(node, node.Identifier.ValueText);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            AddMember(node, node.Identifier.ValueText);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            AddMember(node, $"~{node.Identifier.ValueText}");
            base.VisitDestructorDeclaration(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            AddMember(node, $"operator {node.OperatorToken.ValueText}");
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            AddMember(node, $"operator {node.Type}");
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            AddMember(node, node.Identifier.ValueText);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            AddMember(node, CreateAccessorName(node));
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (node.ExpressionBody is not null)
            {
                AddMember(node, $"get {node.Identifier.ValueText}");
            }

            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (node.ExpressionBody is not null)
            {
                AddMember(node, "get this[]");
            }

            base.VisitIndexerDeclaration(node);
        }

        private void VisitTypeDeclaration(TypeDeclarationSyntax node, Action visitChildren)
        {
            string componentId = SymbolIdentity.CreateType(semanticModel?.GetDeclaredSymbol(node))
                ?? CreateComponentId(
                    node.Identifier.ValueText,
                    node.TypeParameterList?.Parameters.Count ?? 0);
            _componentIds.Push(componentId);
            visitChildren();
            _componentIds.Pop();
        }

        private void AddMember(SyntaxNode node, string memberName)
        {
            if (!_componentIds.TryPeek(out string? componentId) || !HasExecutableBody(node))
            {
                return;
            }

            ComplexityMetric metric = ComplexityMetricCalculator.Calculate(node);
            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            Members.Add(new MemberComplexity(
                componentId,
                memberName,
                new SourceLocation(filePath, span.StartLinePosition.Line + 1),
                metric.CyclomaticComplexity,
                metric.CognitiveComplexity));
        }

        private string CreateComponentId(string name, int arity)
        {
            string typeName = arity == 0 ? name : $"{name}`{arity}";
            return string.IsNullOrWhiteSpace(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
        }

        private static bool HasExecutableBody(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
                ConstructorDeclarationSyntax constructor => constructor.Body is not null || constructor.ExpressionBody is not null,
                DestructorDeclarationSyntax destructor => destructor.Body is not null || destructor.ExpressionBody is not null,
                OperatorDeclarationSyntax operatorDeclaration => operatorDeclaration.Body is not null || operatorDeclaration.ExpressionBody is not null,
                ConversionOperatorDeclarationSyntax conversion => conversion.Body is not null || conversion.ExpressionBody is not null,
                LocalFunctionStatementSyntax localFunction => localFunction.Body is not null || localFunction.ExpressionBody is not null,
                AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
                PropertyDeclarationSyntax property => property.ExpressionBody is not null,
                IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null,
                _ => false,
            };
        }

        private static string CreateAccessorName(AccessorDeclarationSyntax node)
        {
            string accessor = node.Keyword.ValueText;
            return node.Parent?.Parent switch
            {
                PropertyDeclarationSyntax property => $"{accessor} {property.Identifier.ValueText}",
                IndexerDeclarationSyntax => $"{accessor} this[]",
                EventDeclarationSyntax eventDeclaration => $"{accessor} {eventDeclaration.Identifier.ValueText}",
                _ => accessor,
            };
        }
    }

    private sealed record ComplexityMetric(int CyclomaticComplexity, int CognitiveComplexity);

    private sealed class ComplexityMetricCalculator : CSharpSyntaxWalker
    {
        private readonly SyntaxNode _root;
        private int _cyclomaticComplexity = 1;
        private int _cognitiveComplexity;
        private int _nesting;

        private ComplexityMetricCalculator(SyntaxNode root)
        {
            _root = root;
        }

        internal static ComplexityMetric Calculate(SyntaxNode node)
        {
            ComplexityMetricCalculator calculator = new(node);
            calculator.Visit(node);
            return new ComplexityMetric(calculator._cyclomaticComplexity, calculator._cognitiveComplexity);
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            AddNestedDecision();
            Visit(node.Condition);
            VisitWithNesting(node.Statement);
            if (node.Else?.Statement is IfStatementSyntax elseIf)
            {
                Visit(elseIf);
            }
            else if (node.Else?.Statement is StatementSyntax elseStatement)
            {
                VisitWithNesting(elseStatement);
            }
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            AddNestedDecision();
            Visit(node.Declaration);
            foreach (ExpressionSyntax initializer in node.Initializers)
            {
                Visit(initializer);
            }

            Visit(node.Condition);
            foreach (ExpressionSyntax incrementor in node.Incrementors)
            {
                Visit(incrementor);
            }

            VisitWithNesting(node.Statement);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            AddNestedDecision();
            Visit(node.Expression);
            VisitWithNesting(node.Statement);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            AddNestedDecision();
            Visit(node.Variable);
            Visit(node.Expression);
            VisitWithNesting(node.Statement);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            AddNestedDecision();
            Visit(node.Condition);
            VisitWithNesting(node.Statement);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            AddNestedDecision();
            VisitWithNesting(node.Statement);
            Visit(node.Condition);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            AddNestedDecision();
            Visit(node.Declaration);
            Visit(node.Filter);
            VisitWithNesting(node.Block);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            AddNestedDecision();
            Visit(node.Condition);
            VisitWithNesting(node.WhenTrue);
            VisitWithNesting(node.WhenFalse);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            _cognitiveComplexity += 1 + _nesting;
            Visit(node.Expression);
            VisitWithNesting(node.Sections);
        }

        public override void VisitSwitchExpression(SwitchExpressionSyntax node)
        {
            _cognitiveComplexity += 1 + _nesting;
            Visit(node.GoverningExpression);
            VisitWithNesting(node.Arms);
        }

        public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitCaseSwitchLabel(node);
        }

        public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitSwitchExpressionArm(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression))
            {
                _cyclomaticComplexity++;
                _cognitiveComplexity++;
            }

            base.VisitBinaryExpression(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            if (!ReferenceEquals(node, _root))
            {
                // Nested local functions are reported as their own member complexity.
                return;
            }

            base.VisitLocalFunctionStatement(node);
        }

        private void AddNestedDecision()
        {
            _cyclomaticComplexity++;
            _cognitiveComplexity += 1 + _nesting;
        }

        private void VisitWithNesting(SyntaxNode? node)
        {
            if (node is null)
            {
                return;
            }

            _nesting++;
            Visit(node);
            _nesting--;
        }

        private void VisitWithNesting<TNode>(SeparatedSyntaxList<TNode> nodes)
            where TNode : SyntaxNode
        {
            _nesting++;
            foreach (TNode node in nodes)
            {
                Visit(node);
            }

            _nesting--;
        }

        private void VisitWithNesting<TNode>(SyntaxList<TNode> nodes)
            where TNode : SyntaxNode
        {
            _nesting++;
            foreach (TNode node in nodes)
            {
                Visit(node);
            }

            _nesting--;
        }
    }
}
