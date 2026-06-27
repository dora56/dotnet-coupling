# Issue 検出・Severity・循環依存

## 17. Issue 検出設計

### 17.1 IssueType

MVP では `cargo-coupling` v0.3.3 の issue type を基本にする。ただし C# 固有の拡張 issue は後ろに回す。

```csharp
public enum IssueType
{
    // cargo-coupling v0.3.3 aligned core
    GlobalComplexity,
    CascadingChangeRisk,
    InappropriateIntimacy,
    HighEfferentCoupling,
    HighAfferentCoupling,
    CircularDependency,
    HiddenCoupling,
    AccidentalVolatility,
    ScatteredExternalCoupling,

    // v0.2+ / optional design smells
    UnnecessaryAbstraction,
    ShallowModule,
    PassThroughMethod,
    HighCognitiveLoad,
    GodType,
    GodNamespace,
    PrimitiveObsession,
    StaticUtilityHub
}
```

MVP の必須 issue:

- `GlobalComplexity`
- `CascadingChangeRisk`
- `InappropriateIntimacy`
- `HighEfferentCoupling`
- `HighAfferentCoupling`
- `CircularDependency`
- `HiddenCoupling`
- `AccidentalVolatility`
- `ScatteredExternalCoupling`

C# 固有または重めの smell は v0.2 以降に回す。

- `UnnecessaryAbstraction`
- `PrimitiveObsession`
- `GodType`
- `GodNamespace`
- `StaticUtilityHub`
- `ShallowModule`
- `PassThroughMethod`
- `HighCognitiveLoad`

### 17.2 主要 issue

#### Global Complexity

遠い対象に強く依存している。

条件例:

```text
strength >= 0.75 && distance >= 0.50
```

推奨:

- interface を導入する
- 依存先を近づける
- Application service / port を挟む
- project boundary を見直す

#### Cascading Change Risk

変更頻度の高い対象に強く依存している。

条件例:

```text
strength >= 0.75 && volatility >= 0.75
```

推奨:

- volatile な対象の public API を安定化する
- 変更しやすい部分を interface / adapter の裏に隠す
- 依存方向を反転する

#### Inappropriate Intimacy

module / namespace / project 境界を越えて intrusive な結合をしている。

条件例:

```text
strength == Intrusive && distance >= DifferentNamespace
```

C# では private member に通常アクセスできないため、次を intrusive 候補として扱う。

- public mutable field 直接アクセス
- `InternalsVisibleTo` に依存した internal 実装への密結合
- reflection で private / internal member を操作
- service locator で遠方の具象型を取得
- `dynamic` により境界不明な呼び出しをしている

`ConcreteImplementationLeak` は別 issue type にはせず、MVP では `InappropriateIntimacy` または `GlobalComplexity` の説明に統合する。

#### High Efferent Coupling

1つの型または namespace が多くの依存先を持つ。

条件例:

```text
outgoingDependencyCount > thresholds.max_dependencies
```

推奨:

- 責務分割
- facade / coordinator の見直し
- use case 単位に分割

#### High Afferent Coupling

多くの依存元から参照されている。

条件例:

```text
incomingDependentCount > thresholds.max_dependents
```

推奨:

- public API を安定化する
- interface / abstraction を挟む
- 巨大な shared model を分割する

#### Circular Dependency

namespace / project 間で循環依存している。

推奨:

- 双方向依存を interface で反転する
- shared contract を別 project へ切り出す
- domain event / mediator を使う

#### Hidden Coupling

明示的なコード依存はないが、Git 上で頻繁に一緒に変更される。

条件例:

```text
coChangeCount >= thresholds.min_temporal_coupling
&& no_explicit_dependency_between_files
```

推奨:

- 2つの変更が常にセットになる理由を確認する
- 不足している abstraction / domain concept を抽出する
- duplicated logic を統合する

#### Accidental Volatility

supporting / generic subdomain が高頻度で変更されている。

推奨:

- 責務が混ざっていないか確認する
- core domain のロジックが漏れていないか確認する
- framework / infrastructure 詳細が上位層へ漏れていないか確認する

#### Scattered External Coupling

外部 package が多くの内部 namespace / type から直接使われている。

.NET 版では `NuGet package` または `external assembly` の直接使用を対象にする。

条件例:

```text
externalPackageDirectUsers >= thresholds.scattered_external_breadth
```

推奨:

- wrapper / adapter を導入する
- package API を内部の安定 API の裏へ隠す
- upgrade risk を一点に寄せる

#### Unnecessary Abstraction

近くて安定した対象に対して、過剰な interface / abstraction を使っている。

MVP では Low severity または非表示にする。S grade と相性が強いため、過剰最適化の警告文にも使う。

---

## 18. Severity 設計

```csharp
public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}
```

### 18.1 Severity 判定

| 条件 | Severity |
|---|---|
| circular dependency が project boundary をまたぐ | Critical |
| coupling score `< 0.20` | Critical |
| coupling score `< 0.40` | High |
| strong + far + high volatility | High |
| high efferent / afferent coupling | Medium |
| score `< 0.60` | Medium |
| 軽微な smell | Low |

### 18.2 strict mode

デフォルトでは Low を非表示にする。

```bash
dotnet coupling ./src
```

すべて出す場合:

```bash
dotnet coupling --all ./src
```

`--all` は v0.2 で追加してよい。MVP では常に Medium 以上を出す設計でもよい。

---

## 23. 循環依存検出

### 23.1 対象グラフ

MVP では namespace-level graph で循環を検出する。

```text
Namespace A -> Namespace B -> Namespace C -> Namespace A
```

v0.2 以降では project-level graph も追加する。

### 23.2 C# namespace の注意点

C# では以下がありうる。

- 1ファイルに複数 namespace
- 1 namespace が複数ファイルに分散
- file-scoped namespace と block-scoped namespace の混在
- partial class が複数ファイルに分散

したがって、file path ではなく **declared namespace** を node とする。ファイル単位の循環検出はしない。

### 23.3 除外ルール

syntax-only で `using` から graph を作る場合、外部・標準 namespace を除外しないと巨大な SCC が生まれる。MVP では以下を graph edge から除外する。

- `System.*`
- `Microsoft.*` の framework namespace
- `global using` による framework namespace
- primitive / BCL 型
- NuGet package と判定できる外部 namespace

循環検出の対象は、原則として Component Index に存在する内部 namespace のみとする。

### 23.4 アルゴリズム

Tarjan の strongly connected components を使う。

- node: namespace / project
- edge: dependency
- size > 1 の SCC を circular dependency とする
- self-loop は原則無視する

### 23.5 Severity

| 循環タイプ | Severity |
|---|---|
| project 間循環 | Critical |
| namespace 間循環 | High |
| type 間循環 | Medium |

MVP では namespace 間循環を High とする。project 間循環は semantic mode 後に Critical として扱う。
