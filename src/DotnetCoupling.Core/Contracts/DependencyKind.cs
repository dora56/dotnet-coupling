namespace DotnetCoupling.Core;

public enum DependencyKind
{
    Using,
    TypeReference,
    Inheritance,
    InterfaceImplementation,
    GenericConstraint,
    ObjectCreation,
    MethodCall,
    StaticCall,
    FieldAccess,
    PropertyAccess,
    Attribute,
    Reflection,
    Dynamic,
}
