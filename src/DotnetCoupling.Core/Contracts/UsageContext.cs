namespace DotnetCoupling.Core;

public enum UsageContext
{
    UsingDirective,
    Attribute,
    BaseType,
    InterfaceImplementation,
    GenericConstraint,
    FieldType,
    PropertyType,
    ParameterType,
    ReturnType,
    LocalVariableType,
    ObjectCreation,
    MethodCall,
    StaticCall,
    MemberAccess,
    FieldAccess,
    PropertyAccess,
    Reflection,
    DynamicDispatch,
    ServiceLocator,
}
