using DotnetCoupling.Core;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class PathPatternMatcherTests
{
    [Fact]
    public void IsMatch_RootRelativeRecursivePattern_MatchesAbsoluteSegment()
    {
        bool result = PathPatternMatcher.IsMatch("/repo/src/App/Handler.cs", ["src/**"]);

        Assert.True(result);
    }

    [Fact]
    public void IsMatch_TestProjectPattern_DoesNotMatchSubstringDirectory()
    {
        bool result = PathPatternMatcher.IsMatch("/tmp/dotnet-coupling-tests/src/App/Handler.cs", ["tests/**"]);

        Assert.False(result);
    }

    [Fact]
    public void IsMatch_RecursiveTestProjectPattern_MatchesTestDirectorySegment()
    {
        bool result = PathPatternMatcher.IsMatch("/repo/tests/App.Tests/HandlerTests.cs", ["**/tests/**"]);

        Assert.True(result);
    }

    [Fact]
    public void IsMatch_RecursiveExtensionPattern_MatchesNestedFile()
    {
        bool result = PathPatternMatcher.IsMatch("/repo/src/App/Generated.g.cs", ["**/*.g.cs"]);

        Assert.True(result);
    }
}
