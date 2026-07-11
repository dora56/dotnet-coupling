using Microsoft.CodeAnalysis;

namespace DotnetCoupling.Roslyn.Collection;

internal static class SymbolIdentity
{
    internal static string? CreateType(ITypeSymbol? typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType ? CreateType(namedType) : null;
    }

    internal static string? CreateType(INamedTypeSymbol? namedType)
    {
        if (namedType is null || namedType.SpecialType != SpecialType.None)
        {
            return null;
        }

        string typeName = namedType.ContainingType is null
            ? namedType.MetadataName
            : $"{CreateType(namedType.ContainingType)}.{namedType.MetadataName}";
        if (namedType.ContainingType is not null
            || namedType.ContainingNamespace is null
            || namedType.ContainingNamespace.IsGlobalNamespace)
        {
            return typeName;
        }

        return $"{namedType.ContainingNamespace.ToDisplayString()}.{typeName}";
    }

    internal static string? CreateMember(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        INamedTypeSymbol? containingType = symbol.ContainingType;
        string? typeIdentity = CreateType(containingType);
        if (typeIdentity is null)
        {
            return null;
        }

        string memberName = symbol switch
        {
            IMethodSymbol { AssociatedSymbol: ISymbol associated } => associated.Name,
            IMethodSymbol { MethodKind: MethodKind.Constructor } => containingType!.Name,
            IMethodSymbol method => method.Name,
            IPropertySymbol property => property.Name,
            IFieldSymbol field => field.Name,
            IEventSymbol eventSymbol => eventSymbol.Name,
            _ => symbol.Name,
        };
        return $"{typeIdentity}.{memberName}";
    }
}
