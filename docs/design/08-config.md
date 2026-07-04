# 設定ファイル・外部依存の扱い

## 21. 設定ファイル設計

### 21.1 方針

MVP では `.coupling.json` / `coupling.json` のみサポートした。理由は単純で、`System.Text.Json` だけで実装でき、NuGet 依存を増やさずに済むためである。

Phase 5 では developer experience 向上のため、Tomlyn による `.coupling.toml` /
`coupling.toml` を追加した。JSON と TOML は同じ internal config model に正規化し、
validation / unknown property handling / CLI exit code を揃える。

### 21.2 探索順

1. 明示された `--config <file>`
2. `.coupling.json`
3. `coupling.json`
4. `.coupling.toml`
5. `coupling.toml`

既存 JSON auto-discovery の優先順位を維持し、既存 repository の挙動を変えない。
TOML を使いたい場合は `--config .coupling.toml` で明示指定できる。

### 21.3 JSON 例

```json
{
  "$schema": "schemas/dotnet-coupling-config-0.2.schema.json",
  "analysis": {
    "exclude": [
      "**/Generated/**",
      "**/*.g.cs",
      "**/*.generated.cs"
    ],
    "testProjects": [
      "**/tests/**"
    ]
  },
  "thresholds": {
    "maxDependencies": 20,
    "maxDependents": 30,
    "minTemporalCoupling": 3,
    "maxTemporalFilesPerCommit": 50,
    "scatteredExternalBreadth": 5
  },
  "ignore": {
    "paths": ["**/Legacy/**"],
    "namespaces": ["MyApp.Legacy"],
    "issueTypes": ["ScatteredExternalCoupling"],
    "issues": [
      {
        "type": "GlobalComplexity",
        "source": "MyApp.Legacy.LegacyFacade",
        "target": "MyApp.Infrastructure.LegacyRepository",
        "reason": "Accepted legacy adapter until the replacement service ships."
      }
    ]
  },
  "domain": {
    "subdomains": [
      {
        "name": "Billing",
        "category": "core",
        "paths": ["src/MyApp.Billing/**"],
        "expectedVolatility": "high"
      },
      {
        "name": "Reporting",
        "category": "supporting",
        "paths": ["src/MyApp.Reporting/**"],
        "expectedVolatility": "low"
      }
    ]
  }
}
```

`ignore.issues` は baseline と同じ安定キー `(type, source, target)` で特定する。
suppressed issue は grade / issue count / `--check` から除外するが、summary と
JSON には suppressed count と reason を残す。`reason` は必須で、意図しない
負債隠しを避ける。

### 21.4 TOML support

TOML 対応は `Tomlyn` を採用する。Tomlyn は `System.Text.Json` 風の
serializer API と TOML parser を提供し、将来の `Domain Context Config` を
手書きしやすくする。

Design constraints:

- Tomlyn dependency は config loader を持つ production project に閉じる。
- JSON と TOML は同じ `Configuration` model に map する。
- TOML parse error は JSON parse error と同じ config error path で扱う。
- unknown property は JSON と同じく error にし、設定 typo を silent ignore しない。
- `.coupling.toml` と `.coupling.json` の両方がある場合は、auto-discovery では JSON を優先する。TOML を使いたい場合は `--config .coupling.toml` を指定する。
- TOML keys は snake_case のみを標準とし、JSON keys は既存 camelCase を維持する。

```toml
[analysis]
exclude = [
  "**/Generated/**",
  "**/*.g.cs",
  "**/*.generated.cs"
]
test_projects = [
  "**/tests/**"
]

[thresholds]
max_dependencies = 20
max_dependents = 30
min_temporal_coupling = 3
max_temporal_files_per_commit = 50
scattered_external_breadth = 5

[ignore]
paths = ["**/Legacy/**"]
namespaces = ["MyApp.Legacy"]
issue_types = ["ScatteredExternalCoupling"]

[[ignore.issues]]
type = "GlobalComplexity"
source = "MyApp.Legacy.LegacyFacade"
target = "MyApp.Infrastructure.LegacyRepository"
reason = "Accepted legacy adapter until the replacement service ships."

[[domain.subdomains]]
name = "Billing"
category = "core"
paths = ["src/MyApp.Billing/**"]
expected_volatility = "high"

[[domain.subdomains]]
name = "Reporting"
category = "supporting"
paths = ["src/MyApp.Reporting/**"]
expected_volatility = "low"
```

`analysis.testProjects` / `analysis.test_projects` は test project を明示指定する。
該当する source file から出る coupling は観測データとして残すが、active issue 検出
から除外する。テストコードの検証用依存で `GlobalComplexity` /
`InappropriateIntimacy` / `HiddenCoupling` などが増え、production code の設計リスクが
読みにくくなるケースを避けるための設定である。

### 21.5 Domain Context Config

Phase 5 では config file に domain context を追加した。これは subdomain
classification を tool が推論するものではなく、ユーザーが判断した分類を読み込んで
volatility の解釈に使うための設定である。

```json
{
  "domain": {
    "subdomains": [
      {
        "name": "Billing",
        "category": "core",
        "paths": ["src/MyApp.Billing/**"],
        "expectedVolatility": "high"
      },
      {
        "name": "Reporting",
        "category": "supporting",
        "paths": ["src/MyApp.Reporting/**"],
        "expectedVolatility": "low"
      }
    ]
  }
}
```

Semantics:

- `category`: `core` / `supporting` / `generic`
- `expectedVolatility`: `low` / `medium` / `high`
- `name`: 同一 config 内で一意。大文字小文字だけが異なる名前も重複として扱う
- `paths`: repository / workspace root から見た glob。`--config` の場所や config file
  の保存場所では意味を変えない
- 複数 subdomain の `paths` が同じ component に一致した場合は、設定順で最初に一致した
  subdomain を使う
- `core` の high churn は essential business volatility として説明できる
- `core` は `expectedVolatility` が `low` / `medium` でも
  `AccidentalVolatility` としては報告しない
- `supporting` / `generic` の high observed churn は、`expectedVolatility` が
  `low` / `medium` の場合に `AccidentalVolatility` として報告する
- Balance Score / Grade の主計算式は変えない。domain context は追加 issue と説明に使う
- 設定がある場合は summary に `Domain Context: ...` 行を出し、JSON では
  `manifest.domainContext` に subdomain 数、matched / unmatched component 数、
  `AccidentalVolatility` issue 数、subdomain 別 match 数を出す
- 設定がない repository では従来の Git 履歴ベース volatility のみで解析する

### 21.6 Generated code の既定除外

既定除外:

```text
**/bin/**
**/obj/**
**/.git/**
**/.vs/**
**/Generated/**
**/*.g.cs
**/*.generated.cs
**/*.Designer.cs
**/*.AssemblyInfo.cs
**/GlobalUsings.g.cs
```

Source Generator の出力が `obj/` 配下にある場合は既定で除外される。ユーザーが generated code も解析したい場合は v0.2 以降で `includeGenerated` を追加する。

---

## 22. 外部依存の扱い

### 22.1 BCL / framework 型

以下は基本的に issue 対象から除外する。

- `System.*`
- `Microsoft.*` の一部 framework namespace
- primitive types
- `string`, `Guid`, `DateTime`, `Task`, `IEnumerable<T>` などの標準型

ただし、以下は別途 issue として見てもよい。

- `DateTime.Now` への直接依存
- `Guid.NewGuid()` のビジネスロジック内直接使用
- `HttpClient` の乱用
- static global state

MVP では除外し、v0.3 以降の design smell として追加する。

### 22.2 NuGet package

MVP では NuGet package の完全解決はしない。

v0.3 以降で `.csproj` / `project.assets.json` / `packages.lock.json` を読み、外部 package 依存を推定する。

### 22.3 ExternalPackage Distance

semantic mode で外部 assembly と判定できた場合、Distance は `ExternalPackage = 1.0` とする。

ただし外部依存はすべて悪いわけではない。特に安定した framework 型への依存は通常問題にしない。
