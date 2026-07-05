using DotnetCoupling.Core;
using DotnetCoupling.Roslyn;
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
    public void Collect_LocalFunction_ReportsSeparateMemberWithoutInflatingOuterMethod()
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

        MemberComplexity map = Assert.Single(complexities, member => member.MemberName == "Map");
        MemberComplexity local = Assert.Single(complexities, member => member.MemberName == "Local");
        Assert.Equal(4, map.CyclomaticComplexity);
        Assert.Equal(1, map.CognitiveComplexity);
        Assert.Equal(4, local.CyclomaticComplexity);
        Assert.Equal(1, local.CognitiveComplexity);
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
}
