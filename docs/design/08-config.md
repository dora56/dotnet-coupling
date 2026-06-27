# 設定ファイル・外部依存の扱い

## 21. 設定ファイル設計

### 21.1 MVP 方針

MVP では `.coupling.json` / `coupling.json` のみサポートする。理由は単純で、`System.Text.Json` だけで実装でき、NuGet 依存を増やさずに済むためである。

TOML は v0.2 で追加する。`cargo-coupling` との親和性を考えると `.coupling.toml` は魅力的だが、MVP で設定ファイル形式を増やすと本体より設定ローダーの世話が増える。沼は小さいうちに埋める。

### 21.2 探索順

MVP:

1. 明示された `--config <file>`
2. `.coupling.json`
3. `coupling.json`

v0.2 以降:

1. 明示された `--config <file>`
2. `.coupling.toml`
3. `coupling.toml`
4. `.coupling.json`
5. `coupling.json`

`.coupling.toml` が存在するが MVP で未対応の場合は、無視せず warning を出す。

```text
Warning: .coupling.toml was found but TOML config is not supported in v0.1. Use .coupling.json or upgrade when v0.2 is available.
```

### 21.3 JSON 例

```json
{
  "analysis": {
    "excludeTests": true,
    "exclude": [
      "**/bin/**",
      "**/obj/**",
      "**/.git/**",
      "**/.vs/**",
      "**/Generated/**",
      "**/*.g.cs",
      "**/*.generated.cs",
      "**/*.Designer.cs",
      "**/*.AssemblyInfo.cs",
      "**/GlobalUsings.g.cs"
    ]
  },
  "thresholds": {
    "maxDependencies": 20,
    "maxDependents": 30,
    "strongCoupling": 0.75,
    "farDistance": 0.50,
    "highVolatility": 0.75,
    "minTemporalCoupling": 3,
    "maxTemporalFilesPerCommit": 50,
    "scatteredExternalBreadth": 5
  },
  "volatility": {
    "high": ["src/MyApp.Domain/Core/**"],
    "low": ["src/MyApp.Infrastructure/Shared/**"]
  },
  "subdomains": {
    "core": ["src/MyApp.Domain/**"],
    "supporting": ["src/MyApp.Application/**"],
    "generic": ["src/MyApp.Infrastructure/**"]
  }
}
```

### 21.4 TOML 例 v0.2+

TOML 対応時は `Tomlyn` を第一候補にする。

```toml
[analysis]
exclude_tests = true
exclude = [
  "**/bin/**",
  "**/obj/**",
  "**/.git/**",
  "**/.vs/**",
  "**/Generated/**",
  "**/*.g.cs",
  "**/*.generated.cs",
  "**/*.Designer.cs",
  "**/*.AssemblyInfo.cs",
  "**/GlobalUsings.g.cs"
]

[thresholds]
max_dependencies = 20
max_dependents = 30
strong_coupling = 0.75
far_distance = 0.50
high_volatility = 0.75
min_temporal_coupling = 3
max_temporal_files_per_commit = 50
scattered_external_breadth = 5

[volatility]
high = ["src/MyApp.Domain/Core/**"]
low = ["src/MyApp.Infrastructure/Shared/**"]

[subdomains]
core = ["src/MyApp.Domain/**"]
supporting = ["src/MyApp.Application/**"]
generic = ["src/MyApp.Infrastructure/**"]
```

### 21.5 Generated code の既定除外

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

v0.2 以降で `.csproj` / `project.assets.json` / `packages.lock.json` を読み、外部 package 依存を推定する。

### 22.3 ExternalPackage Distance

semantic mode で外部 assembly と判定できた場合、Distance は `ExternalPackage = 1.0` とする。

ただし外部依存はすべて悪いわけではない。特に安定した framework 型への依存は通常問題にしない。
