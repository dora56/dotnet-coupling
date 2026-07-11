using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetCoupling.Roslyn.Collection;

internal sealed class CSharpCyclomaticComplexityCalculator : CSharpSyntaxWalker
{
    private readonly SyntaxNode _root;
    private int _complexity;

    private CSharpCyclomaticComplexityCalculator(SyntaxNode root)
    {
        _root = root;
        _complexity = HasExecutableEntryCost(root) ? 1 : 0;
    }

    internal static int Calculate(SyntaxNode root)
    {
        CSharpCyclomaticComplexityCalculator calculator = new(root);
        calculator.Visit(root);
        return calculator._complexity;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        Increment();
        base.VisitIfStatement(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Increment();
        base.VisitConditionalExpression(node);
    }

    public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        Increment();
        base.VisitConditionalAccessExpression(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        Increment();
        base.VisitWhileStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        Increment();
        base.VisitDoStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        Increment();
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        Increment();
        base.VisitForEachStatement(node);
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        Increment();
        base.VisitForEachVariableStatement(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression)
            || node.IsKind(SyntaxKind.LogicalOrExpression)
            || node.IsKind(SyntaxKind.CoalesceExpression))
        {
            Increment();
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.CoalesceAssignmentExpression))
        {
            Increment();
        }

        base.VisitAssignmentExpression(node);
    }

    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
    {
        Increment();
        base.VisitCaseSwitchLabel(node);
    }

    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        Increment();
        base.VisitSwitchExpressionArm(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        if (node.IsKind(SyntaxKind.AndPattern) || node.IsKind(SyntaxKind.OrPattern))
        {
            Increment();
        }

        base.VisitBinaryPattern(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (!ReferenceEquals(node, _root) && node.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        base.VisitLocalFunctionStatement(node);
    }

    private void Increment() => _complexity++;

    private static bool HasExecutableEntryCost(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax
            or PropertyDeclarationSyntax
            or IndexerDeclarationSyntax;
    }
}
