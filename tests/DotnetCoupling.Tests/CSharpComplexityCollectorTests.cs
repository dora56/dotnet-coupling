using DotnetCoupling.Core;
using DotnetCoupling.Roslyn.Collection;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CSharpComplexityCollectorTests
{
    [Fact]
    public void Collect_NestedBranchesAndBooleanBranch_CountsCyclomaticAndCognitiveComplexity()
    {
        string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle(int value)
                {
                    if (value > 0 && value < 10)
                    {
                        foreach (int item in new[] { 1, 2 })
                        {
                            if (item == value)
                            {
                                return;
                            }
                        }
                    }
                }

                public int Simple => 1;
            }
            """;
        IReadOnlyList<MemberComplexity> complexities = CollectMembers(source);

        MemberComplexity handle = Assert.Single(complexities, member => member.MemberName == "Handle");
        Assert.Equal("Sample.Handler", handle.ComponentId);
        Assert.Equal(5, handle.CyclomaticComplexity);
        Assert.Equal(7, handle.CognitiveComplexity);
        MemberComplexity property = Assert.Single(complexities, member => member.MemberName == "get Simple");
        Assert.Equal(1, property.CyclomaticComplexity);
        Assert.Equal(0, property.CognitiveComplexity);
    }

    [Fact]
    public void Collect_NonStaticLocalFunction_IsAttributedToOuterMethod()
    {
        string source = """
            namespace Sample;

            public sealed class Handler
            {
                public int Map(int value)
                {
                    int Local(int input)
                    {
                        return input switch
                        {
                            1 => 1,
                            2 => 2,
                            _ => 0,
                        };
                    }

                    return value switch
                    {
                        1 => Local(value),
                        2 => 2,
                        _ => 0,
                    };
                }
            }
            """;
        IReadOnlyList<MemberComplexity> complexities = CollectMembers(source);

        MemberComplexity map = Assert.Single(complexities);
        Assert.Equal("Map", map.MemberName);
        Assert.Equal(7, map.CyclomaticComplexity);
        Assert.Equal(3, map.CognitiveComplexity);
    }

    [Fact]
    public void Collect_IfElseIfElse_CountsHybridBranchesWithoutNestingPenalty()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle(bool first, bool second)
                {
                    if (first)
                    {
                    }
                    else if (second)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(3, member.CyclomaticComplexity);
        Assert.Equal(3, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_LogicalOperatorSequences_CountsCognitiveSequenceChanges()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public bool Same(bool a, bool b, bool c) => a && b && c;

                public bool Mixed(bool a, bool b, bool c, bool d) => a && b || c && d;
            }
            """;

        IReadOnlyList<MemberComplexity> members = CollectMembers(source);
        MemberComplexity same = Assert.Single(members, member => member.MemberName == "Same");
        MemberComplexity mixed = Assert.Single(members, member => member.MemberName == "Mixed");

        Assert.Equal(3, same.CyclomaticComplexity);
        Assert.Equal(1, same.CognitiveComplexity);
        Assert.Equal(4, mixed.CyclomaticComplexity);
        Assert.Equal(3, mixed.CognitiveComplexity);
    }

    [Fact]
    public void Collect_ParenthesizedLogicalSequences_PreservesSonarSequenceBoundaries()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public bool Chained(bool a, bool b, bool c, bool d) => a && b && (c && d);

                public bool Negated(bool a, bool b, bool c, bool d) => a && !(b && c) && d;
            }
            """;

        IReadOnlyList<MemberComplexity> members = CollectMembers(source);
        MemberComplexity chained = Assert.Single(members, member => member.MemberName == "Chained");
        MemberComplexity negated = Assert.Single(members, member => member.MemberName == "Negated");

        Assert.Equal(4, chained.CyclomaticComplexity);
        Assert.Equal(1, chained.CognitiveComplexity);
        Assert.Equal(4, negated.CyclomaticComplexity);
        Assert.Equal(2, negated.CognitiveComplexity);
    }

    [Fact]
    public void Collect_NestedFlowBreaks_AddsCognitiveNestingDepth()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle(bool enabled)
                {
                    if (enabled)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            while (enabled)
                            {
                                return;
                            }
                        }
                    }
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(4, member.CyclomaticComplexity);
        Assert.Equal(6, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_Lambda_AddsCognitiveNestingWithoutCyclomaticEntryCost()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public bool Handle(int value)
                {
                    System.Func<int, bool> predicate = item =>
                    {
                        if (item > 0)
                        {
                            return true;
                        }

                        return false;
                    };
                    return predicate(value);
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(2, member.CyclomaticComplexity);
        Assert.Equal(2, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_StaticLocalFunction_ReportsSeparateNestedMember()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle()
                {
                    static bool Local(int value)
                    {
                        if (value > 0)
                        {
                            return true;
                        }

                        return false;
                    }

                    _ = Local(1);
                }
            }
            """;

        IReadOnlyList<MemberComplexity> members = CollectMembers(source);
        MemberComplexity outer = Assert.Single(members, member => member.MemberName == "Handle");
        MemberComplexity local = Assert.Single(members, member => member.MemberName == "Local");

        Assert.Equal(1, outer.CyclomaticComplexity);
        Assert.Equal(0, outer.CognitiveComplexity);
        Assert.Equal(2, local.CyclomaticComplexity);
        Assert.Equal(2, local.CognitiveComplexity);
    }

    [Fact]
    public void Collect_DirectRecursion_AddsCognitiveIncrement()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public int Factorial(int value)
                {
                    if (value <= 1)
                    {
                        return 1;
                    }

                    return value * Factorial(value - 1);
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(2, member.CyclomaticComplexity);
        Assert.Equal(2, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_GotoInsideBranch_AddsNestedCognitiveIncrementOnly()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle(bool enabled)
                {
                    if (enabled)
                    {
                        goto Done;
                    }

                Done:
                    return;
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(2, member.CyclomaticComplexity);
        Assert.Equal(3, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_NullFlowOperators_CountsCyclomaticOnly()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public string Handle(string? value, string fallback)
                {
                    value ??= fallback;
                    return value?.Trim() ?? fallback;
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(4, member.CyclomaticComplexity);
        Assert.Equal(0, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_Catch_CountsCognitiveButNotCyclomatic()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public void Handle()
                {
                    try
                    {
                    }
                    catch (System.Exception)
                    {
                    }
                    finally
                    {
                    }
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(1, member.CyclomaticComplexity);
        Assert.Equal(1, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_SwitchExpressionAndPatterns_CountsSonarBranches()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public int Map(int value) => value switch
                {
                    > 0 and < 10 or 42 => 1,
                    100 => 2,
                    _ => 0,
                };
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(6, member.CyclomaticComplexity);
        Assert.Equal(3, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_RightAssociatedSamePattern_CountsOneCognitiveSequence()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                public bool Matches(int value) => value is > 0 and (< 10 and not 5);
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal(3, member.CyclomaticComplexity);
        Assert.Equal(1, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_AccessorSwitchStatement_CountsSwitchOnceAndCaseBranches()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                private int value;

                public int Value
                {
                    get
                    {
                        switch (value)
                        {
                            case 1:
                                return 1;
                            case 2:
                                return 2;
                            default:
                                return 0;
                        }
                    }
                }
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal("get Value", member.MemberName);
        Assert.Equal(3, member.CyclomaticComplexity);
        Assert.Equal(1, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_FieldInitializer_UsesSonarFieldScope()
    {
        const string source = """
            namespace Sample;

            public sealed class Handler
            {
                private readonly System.Func<int, bool> predicate = value =>
                {
                    if (value > 0)
                    {
                        return true;
                    }

                    return false;
                };
            }
            """;

        MemberComplexity member = Assert.Single(CollectMembers(source));

        Assert.Equal("predicate", member.MemberName);
        Assert.Equal(1, member.CyclomaticComplexity);
        Assert.Equal(2, member.CognitiveComplexity);
    }

    [Fact]
    public void Collect_ExtendingSameLogicalSequence_DoesNotIncreaseCognitiveComplexity()
    {
        Prop.ForAll<int>(value =>
        {
            int operandCount = Math.Abs(value % 8) + 2;
            string expression = string.Join(" && ", Enumerable.Range(0, operandCount).Select(index => $"values[{index}]"));
            string source = CreateExpressionMethod(expression, operandCount);

            MemberComplexity member = Assert.Single(CollectMembers(source));

            return member.CognitiveComplexity == 1
                && member.CyclomaticComplexity == operandCount;
        }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Collect_AlternatingLogicalOperators_CountsEachSequence()
    {
        Prop.ForAll<int>(value =>
        {
            int operandCount = Math.Abs(value % 8) + 2;
            string expression = "values[0]";
            for (int index = 1; index < operandCount; index++)
            {
                string logicalOperator = index % 2 == 1 ? "&&" : "||";
                expression = $"({expression} {logicalOperator} values[{index}])";
            }
            string source = CreateExpressionMethod(expression, operandCount);

            MemberComplexity member = Assert.Single(CollectMembers(source));

            return member.CognitiveComplexity == operandCount - 1
                && member.CyclomaticComplexity == operandCount;
        }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Collect_AddingNesting_NeverDecreasesCognitiveComplexity()
    {
        Prop.ForAll<int>(value =>
        {
            int depth = Math.Abs(value % 6) + 1;
            string nested = "return;";
            for (int index = 0; index < depth; index++)
            {
                nested = $"if (enabled) {{ {nested} }}";
            }

            string source = $$"""
                namespace Sample;

                public sealed class Handler
                {
                    public void Handle(bool enabled)
                    {
                        {{nested}}
                    }
                }
                """;
            MemberComplexity member = Assert.Single(CollectMembers(source));
            int expected = depth * (depth + 1) / 2;

            return member.CognitiveComplexity == expected
                && member.CyclomaticComplexity == depth + 1;
        }).QuickCheckThrowOnFailure();
    }

    private static IReadOnlyList<MemberComplexity> CollectMembers(string source)
    {
        const string filePath = "/tmp/Handler.cs";
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            source,
            path: filePath,
            cancellationToken: TestContext.Current.CancellationToken);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot(TestContext.Current.CancellationToken);
        return CSharpComplexityCollector.Collect(tree, root, "Sample", filePath);
    }

    private static string CreateExpressionMethod(string expression, int operandCount)
    {
        string values = string.Join(", ", Enumerable.Repeat("true", operandCount));
        return $$"""
            namespace Sample;

            public sealed class Handler
            {
                public bool Handle()
                {
                    bool[] values = [{{values}}];
                    return {{expression}};
                }
            }
            """;
    }
}
