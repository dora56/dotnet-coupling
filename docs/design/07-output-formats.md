# レポート出力設計

## 19. レポート設計

### 19.1 通常出力

```text
Analyzing project at './src'...
Analysis complete: 128 files, 342 types

Grade: B (Healthy) | Avg Score: 0.84 | Issues: 0 Critical, 2 High, 8 Medium
Grade basis: issue-density across 891 internal couplings

Top Issues
────────────────────────────────────────────────────────────
1. 🟠 MyApp.Api.UsersController -> MyApp.Infrastructure.SqlUserRepository
   Type: Inappropriate Intimacy
   Severity: High
   Score: 0.31
   Problem: API layer directly depends on infrastructure concrete implementation.
   Fix: Depend on IUserRepository and register SqlUserRepository in composition root.

2. 🟡 MyApp.Application.CreateOrderHandler -> MyApp.Domain.Order
   Type: Global Complexity
   Severity: Medium
   Score: 0.52
   Problem: Strong dependency to a volatile domain model across namespace boundary.
   Fix: Review whether this dependency should be closer or abstracted.
```

### 19.2 Summary 出力

```text
Grade: B | Avg Score: 0.84 | Basis: issue-density
Files: 128 | Types: 342 | Couplings: 891 internal / 143 external
Issues: 0 Critical, 2 High, 8 Medium
Mode: semantic-preview
```

`Mode:` 行は `syntax-only` 以外の mode でのみ表示する。

S grade の場合は必ず warning を出す。

```text
Grade: S (Over-optimized warning)
This is not a trophy. It may mean the project is over-abstracted or the thresholds are too strict.
```

### 19.3 JSON 出力

JSON は v0.1 から `$schema` と `schemaVersion` を含める。`1.0.0` までは experimental だが、破壊的変更は changelog に書く。

```json
{
  "$schema": "https://raw.githubusercontent.com/YOUR_GITHUB/dotnet-coupling/main/schemas/dotnet-coupling-report.schema.json",
  "schemaVersion": "0.1",
  "tool": "dotnet-coupling",
  "version": "0.2.0-alpha.1",
  "analysis": {
    "path": "./src",
    "mode": "semantic-preview",
    "files": 128,
    "components": 342,
    "couplings": {
      "total": 1034,
      "internal": 891,
      "external": 143
    },
    "gitUsed": true,
    "gitMonths": 6
  },
  "grade": {
    "letter": "B",
    "display": "Healthy",
    "basis": "issue-density",
    "rationale": "Medium issue density is manageable and no critical issue was found."
  },
  "scores": {
    "averageBalanceScore": 0.84
  },
  "issueCounts": {
    "critical": 0,
    "high": 2,
    "medium": 8,
    "low": 12
  },
  "issues": [
    {
      "type": "InappropriateIntimacy",
      "severity": "High",
      "source": "MyApp.Api.UsersController",
      "target": "MyApp.Infrastructure.SqlUserRepository",
      "score": 0.31,
      "problem": "API layer directly depends on infrastructure concrete implementation.",
      "recommendation": "Depend on IUserRepository and register SqlUserRepository in composition root.",
      "location": {
        "file": "src/MyApp.Api/UsersController.cs",
        "line": 42
      }
    }
  ],
  "manifest": {
    "confidence": "semantic-preview",
    "runNotes": [
      "Semantic mode uses MSBuildWorkspace preview loading.",
      "Some symbol resolution features are still syntax-equivalent."
    ],
    "blindSpots": [
      {
        "kind": "RuntimeDependency",
        "description": "DI container runtime resolution is not analyzed in syntax-only mode."
      },
      {
        "kind": "DynamicDispatch",
        "description": "Reflection and dynamic calls may be incomplete."
      },
      {
        "kind": "GeneratedCode",
        "description": "Generated code is excluded by default."
      }
    ],
    "diagnostics": [
      {
        "code": "missing-project-reference",
        "severity": "Warning",
        "message": "Referenced project was not found: /repo/src/Missing/Missing.csproj",
        "path": "/repo/src/App/App.csproj"
      }
    ]
  }
}
```

### 19.4 JSON Schema 方針

`supported schemaVersion` は minor 単位で管理する。

```text
schemas/
  dotnet-coupling-report-0.1.schema.json
  dotnet-coupling-report.schema.json -> latest experimental
```

CI 利用者向けに、少なくとも以下は安定させる。

- `grade.letter`
- `grade.basis`
- `issueCounts`
- `issues[].type`
- `issues[].severity`
- `issues[].source`
- `issues[].target`
- `issues[].location`
- `manifest.blindSpots`
- `manifest.diagnostics` は optional field として後方互換を保ちながら追加できる

### 19.5 出力モード優先順位

同時指定された場合の優先順位:

1. `--json`
2. `--check`
3. `--summary`
4. 通常レポート

競合するモードが指定された場合は warning を出す。
