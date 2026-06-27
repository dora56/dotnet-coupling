# データモデル

## 11. データモデル

### 11.1 Component

```csharp
public sealed record Component(
    string Id,
    string Name,
    string Namespace,
    string? ProjectName,
    string FilePath,
    ComponentKind Kind,
    Visibility Visibility);
```

```csharp
public enum ComponentKind
{
    Class,
    Record,
    Struct,
    Interface,
    Enum,
    Delegate,
    Namespace,
    Project,
    ExternalPackage
}
```

### 11.2 Visibility

C# の可視性は Rust の `pub(crate)` と完全には対応しないため、C# 固有の値を持つ。

```csharp
public enum Visibility
{
    Public,
    Internal,
    Protected,
    ProtectedInternal,
    PrivateProtected,
    Private
}
```

基本方針:

| Visibility | 扱い |
|---|---|
| `Public` | public API。原則 intrusive ではない |
| `Internal` | assembly 内 API。別 project / assembly から見えている場合は境界漏れの疑い |
| `Protected` | 継承階層向け API。継承が絡む coupling として扱う |
| `ProtectedInternal` | 同 assembly または派生型からアクセス可能。広めなので注意対象 |
| `PrivateProtected` | 同 assembly の派生型のみ。狭いが継承密結合の疑い |
| `Private` | 通常は外部アクセス不可。reflection / nested type 経由の疑いがある場合のみ intrusive |

MVP の syntax-only では「本当に別 assembly から見えているか」を完全には判断できない。v0.3 の semantic mode で project / assembly 境界と合わせて精度を上げる。

### 11.3 UsageContext

`UsageContext` は「依存がどの文脈で現れたか」を表す。これは `IntegrationStrength` を決めるための入力である。

```csharp
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
    ServiceLocator
}
```

初期マッピング:

| UsageContext | IntegrationStrength |
|---|---|
| `InterfaceImplementation`, `GenericConstraint` | Contract |
| `BaseType` + abstract class | Contract を既定とする。設定で Functional に切り替え可能 |
| `FieldType`, `PropertyType`, `ParameterType`, `ReturnType`, `LocalVariableType`, `Attribute` | Model |
| `ObjectCreation`, `MethodCall`, `StaticCall`, `PropertyAccess` | Functional |
| `BaseType` + concrete class | Functional |
| `FieldAccess`, `Reflection`, `DynamicDispatch`, `ServiceLocator` | Intrusive |

interface 型への `ParameterType` / `FieldType` は Contract に補正してよい。syntax-only では interface 判定が曖昧なため、Component Index で `InterfaceDeclarationSyntax` と照合できた場合のみ Contract にする。

`BaseType` は一律 Functional にしない。MVP の syntax-only でも、依存先型の宣言に `abstract` modifier があるかは Component Index から判定できる。したがって既定は以下とする。

```text
BaseType + abstract modifier -> Contract
BaseType + no abstract modifier -> Functional
```

ただし abstract class を契約と見るか具象継承と見るかはチーム設計によって異なるため、v0.2 以降で `abstractBaseTypeStrength` のような設定を追加できるようにする。

### 11.4 DependencyObservation

構文解析で見つかった個別の依存事実。

```csharp
public sealed record DependencyObservation(
    string SourceComponentId,
    string TargetName,
    DependencyKind Kind,
    UsageContext Usage,
    string FilePath,
    int Line,
    string? Expression);
```

```csharp
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
    Dynamic
}
```

### 11.5 CouplingMetrics

解決済みの結合関係。

```csharp
public sealed record CouplingMetrics(
    string Source,
    string Target,
    IntegrationStrength Strength,
    Distance Distance,
    Volatility Volatility,
    string? SourceProject,
    string? TargetProject,
    Visibility TargetVisibility,
    SourceLocation Location);
```

### 11.6 BalanceScore

```csharp
public sealed record BalanceScore(
    CouplingMetrics Coupling,
    double Score,
    double Alignment,
    double VolatilityImpact,
    BalanceInterpretation Interpretation);
```

### 11.7 CouplingIssue

```csharp
public sealed record CouplingIssue(
    IssueType Type,
    Severity Severity,
    string Source,
    string Target,
    double Score,
    string Problem,
    string Recommendation,
    SourceLocation? Location);
```
