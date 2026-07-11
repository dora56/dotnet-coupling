using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Roslyn.Collection;

internal sealed class CSharpCognitiveComplexityCalculator : CSharpSyntaxWalker
{
    private readonly SyntaxNode _root;
    private readonly HashSet<SyntaxNode> _logicalOperationsToIgnore = [];
    private readonly MethodDeclarationSyntax? _method;
    private int _complexity;
    private int _nesting;
    private bool _hasDirectRecursiveCall;

    private CSharpCognitiveComplexityCalculator(SyntaxNode root)
    {
        _root = root;
        _method = root as MethodDeclarationSyntax;
    }

    internal static int Calculate(SyntaxNode root)
    {
        CSharpCognitiveComplexityCalculator calculator = new(root);
        calculator.Visit(root);
        if (calculator._hasDirectRecursiveCall)
        {
            calculator._complexity++;
        }

        return calculator._complexity;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        if (node.Parent is ElseClauseSyntax)
        {
            base.VisitIfStatement(node);
            return;
        }

        AddNestedDecisionAndVisit(() => base.VisitIfStatement(node));
    }

    public override void VisitElseClause(ElseClauseSyntax node)
    {
        _complexity++;
        base.VisitElseClause(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitConditionalExpression(node));
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitSwitchStatement(node));
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitSwitchExpression(node));
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitForStatement(node));
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitForEachStatement(node));
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitForEachVariableStatement(node));
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitWhileStatement(node));
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitDoStatement(node));
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        AddNestedDecisionAndVisit(() => base.VisitCatchClause(node));
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        SyntaxKind kind = node.Kind();
        if ((kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.LogicalOrExpression)
            && !_logicalOperationsToIgnore.Contains(node))
        {
            if (!UnwrapParentheses(node.Left).IsKind(kind))
            {
                _complexity++;
            }

            ExpressionSyntax right = UnwrapParentheses(node.Right);
            if (right.IsKind(kind))
            {
                _logicalOperationsToIgnore.Add(right);
            }
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        SyntaxKind kind = node.Kind();
        if ((kind == SyntaxKind.AndPattern || kind == SyntaxKind.OrPattern)
            && !_logicalOperationsToIgnore.Contains(node))
        {
            if (!UnwrapParentheses(node.Left).IsKind(kind))
            {
                _complexity++;
            }

            PatternSyntax right = UnwrapParentheses(node.Right);
            if (right.IsKind(kind))
            {
                _logicalOperationsToIgnore.Add(right);
            }
        }

        base.VisitBinaryPattern(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_method is not null
            && node.Expression is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == _method.Identifier.ValueText
            && node.ArgumentList.Arguments.Count == _method.ParameterList.Parameters.Count)
        {
            _hasDirectRecursiveCall = true;
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitGotoStatement(GotoStatementSyntax node)
    {
        _complexity += 1 + _nesting;
        base.VisitGotoStatement(node);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        VisitWithNesting(() => base.VisitSimpleLambdaExpression(node));
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        VisitWithNesting(() => base.VisitParenthesizedLambdaExpression(node));
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (!ReferenceEquals(node, _root) && node.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        VisitWithNesting(() => base.VisitLocalFunctionStatement(node));
    }

    private void AddNestedDecisionAndVisit(Action visit)
    {
        _complexity += 1 + _nesting;
        VisitWithNesting(visit);
    }

    private void VisitWithNesting(Action visit)
    {
        _nesting++;
        visit();
        _nesting--;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static PatternSyntax UnwrapParentheses(PatternSyntax pattern)
    {
        while (pattern is ParenthesizedPatternSyntax parenthesized)
        {
            pattern = parenthesized.Pattern;
        }

        return pattern;
    }
}
