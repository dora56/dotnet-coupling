using DotnetCoupling.Cli.Analysis;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CSharpSyntaxDependencyCollectorTests
{
    [Fact]
    public void AnalyzeFile_MixedTypeDeclarationsAndTypeShapes_CollectsComponentsAndObservations()
    {
        string filePath = WriteSource("""
            namespace Sample.Syntax
            {
                using Alias = System.Text.StringBuilder;
                using System.Collections.Generic;

                public interface IPort
                {
                }

                record Model
                {
                }

                public struct Value
                {
                    private Model[] _items;
                    public Model? Maybe { get; }
                    public List<Model> Create(IPort port)
                    {
                        return new List<Model>();
                    }
                }

                public enum Status
                {
                    Ready
                }

                public class VisibilityHost
                {
                    private protected class PrivateProtectedNested
                    {
                    }

                    protected internal class ProtectedInternalNested
                    {
                    }

                    protected class ProtectedNested
                    {
                    }

                    private class PrivateNested
                    {
                    }
                }
            }
            """);

        SyntaxFileAnalysis analysis = CSharpSyntaxDependencyCollector.AnalyzeFile(filePath);

        AssertComponent(analysis, "IPort", ComponentKind.Interface, Visibility.Public);
        AssertComponent(analysis, "Model", ComponentKind.Record, Visibility.Internal);
        AssertComponent(analysis, "Value", ComponentKind.Struct, Visibility.Public);
        AssertComponent(analysis, "Status", ComponentKind.Enum, Visibility.Public);
        AssertComponent(analysis, "PrivateProtectedNested", ComponentKind.Class, Visibility.PrivateProtected);
        AssertComponent(analysis, "ProtectedInternalNested", ComponentKind.Class, Visibility.ProtectedInternal);
        AssertComponent(analysis, "ProtectedNested", ComponentKind.Class, Visibility.Protected);
        AssertComponent(analysis, "PrivateNested", ComponentKind.Class, Visibility.Private);

        UsingNamespace usingNamespace = Assert.Single(analysis.UsingNamespaces);
        Assert.Equal("System.Collections.Generic", usingNamespace.Name);

        Assert.Contains(analysis.Observations, observation =>
            observation.SourceComponentId == "Sample.Syntax.Value"
            && observation.TargetName == "Model"
            && observation.Usage == UsageContext.FieldType);
        Assert.Contains(analysis.Observations, observation =>
            observation.SourceComponentId == "Sample.Syntax.Value"
            && observation.TargetName == "Model"
            && observation.Usage == UsageContext.PropertyType);
        Assert.Contains(analysis.Observations, observation =>
            observation.SourceComponentId == "Sample.Syntax.Value"
            && observation.TargetName == "IPort"
            && observation.Usage == UsageContext.ParameterType);
        Assert.Contains(analysis.Observations, observation =>
            observation.SourceComponentId == "Sample.Syntax.Value"
            && observation.TargetName == "List`1"
            && observation.Usage == UsageContext.ObjectCreation);
    }

    private static void AssertComponent(
        SyntaxFileAnalysis analysis,
        string name,
        ComponentKind kind,
        Visibility visibility)
    {
        Component component = Assert.Single(analysis.Components, component => component.Name == name);
        Assert.Equal(kind, component.Kind);
        Assert.Equal(visibility, component.Visibility);
        Assert.Equal("Sample.Syntax", component.Namespace);
    }

    private static string WriteSource(string source)
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "Sample.cs");
        File.WriteAllText(filePath, source);
        return filePath;
    }
}
