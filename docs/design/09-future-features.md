# 将来機能と Phase 4+ 設計 (Baseline / Hotspots / AI / SARIF)

## 24. Baseline / Ratchet Gate 設計

v0.2 で実装済み。

### 24.1 目的

既存の負債で CI を赤くし続けるのではなく、**新しく増えた悪い結合だけを止める**。

### 24.2 コマンド

```bash
dotnet coupling --baseline main ./src
```

```bash
dotnet coupling --check --baseline main --fail-on high ./src
```

### 24.3 issue key

baseline diff では以下を安定キーにする。

```text
(issue_type, source, target)
```

可能なら location は補助情報にする。line number は変わりやすいため key にしない。

### 24.4 出力例

```text
Baseline: main
New Issues: 2 High, 3 Medium
Resolved Issues: 1 Medium
Unchanged Issues: 12

FAIL: 2 new High issues found.
```

---

## 25. Hotspots / Impact / Trace 設計

### 25.0 TOML Config Support

Phase 5 では、developer experience 向上のため Tomlyn による TOML config support を
追加した。TOML は既存 JSON config と同じ internal config model に正規化する。

Target files:

- `.coupling.toml`
- `coupling.toml`
- explicit `--config <file>.toml`

Design:

- JSON と TOML は同じ internal config model に正規化する。
- JSON の既存 auto-discovery 優先順位は変えない。
- TOML は将来の `Domain Context Config` を手書きしやすくするための入力形式であり、
  JSON schema / JSON report の互換性とは独立に扱う。
- unknown property / invalid enum は JSON config と同じ validation
  error として扱い、silent ignore しない。
- TOML keys は snake_case のみを標準とし、JSON keys は既存 camelCase を維持する。

### 25.1 Domain Context Config

Phase 5 では、ユーザーが設定した domain context を読み込み、Git 履歴から観測した
churn を「本質的な業務変化による揮発性」か「設計 / 実装の摩擦による churn」かに
分けて解釈できるようにする。

この tool は subdomain classification を自動推論しない。Core / Supporting /
Generic の分類は組織戦略、競争優位、時期によって変わるため、ユーザーが設定で
明示したものだけを根拠にする。

設定例:

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
      },
      {
        "name": "IdentityProvider",
        "category": "generic",
        "paths": ["src/MyApp.IdentityProvider/**"],
        "expectedVolatility": "low"
      }
    ]
  }
}
```

用語:

- `observedChurn`: Git 履歴から観測した変更頻度
- `expectedVolatility`: 設定から読み込んだ業務上期待される揮発性
- `essentialVolatility`: core subdomain などで、product model の進化として説明できる高い揮発性
- `accidentalChurn`: supporting / generic など本来安定してほしい領域で観測される高 churn

0.5.0 初期契約:

- Domain Context がある場合、Balance Score は target subdomain の
  `expectedVolatility` を優先する。
- core の high churn は、それ自体を issue にしない。ただし core に遠く強く依存している component は、core の本質的な揮発性により引き続き risk が高い。
- supporting / generic の high observed churn は `AccidentalVolatility` issue として表示する。
- `.coupling.json` に設定がない場合は従来どおり Git 履歴ベースの volatility のみで動作する。

後続拡張:

- Hotspots / Markdown / AI output に `essentialVolatility` / `accidentalChurn` の reason を追加する。
- JSON schema には optional fields として `domainContext`, `observedChurn`, `expectedVolatility`, `volatilityKind` を追加する。

### 25.2 Hotspots

Phase 4 で `--hotspots [N]` として実装する。リファクタリング候補をランキングする。
Hotspots は remediation priority であり、Grade は issue density による project
health gate として分離する。

```bash
dotnet-coupling --hotspots 10 ./src
```

スコア要素:

- issue severity
- incoming dependency count
- outgoing dependency count
- volatility
- circular dependency participation
- project boundary crossing
- `0.5.0`: cyclomatic complexity / cognitive complexity

`0.5.0` では complexity-assisted ranking を `--hotspots` に限定して追加する。
これは Grade とは別の priority score であり、complexity が高いだけでは issue
severity や `--check` を変えない。threshold / weight の config 化、`--impact` や
`--ai` への展開は後続スライスに残す。

### 25.3 Impact

指定コンポーネントを変更した場合の影響範囲を出す。

```bash
dotnet coupling --impact MyApp.Domain.User ./src
```

出力:

- 直接依存元
- 間接依存元
- risk score
- 影響 project / namespace

### 25.4 Trace

指定 symbol への依存を追う。

```bash
dotnet coupling --trace IUserRepository ./src
```

MVP の syntax-only では精度が出にくいため、semantic mode 後に実装する。

---

## 26. AI 出力設計

v0.5 以降で `--ai` を追加する。

目的は AI coding agent に渡しやすい形で issue を整理すること。

```bash
dotnet coupling --ai ./src
```

出力方針:

- issue の原因を簡潔に説明
- 対象ファイルと行番号を出す
- 期待する修正方針を明示する
- いきなり巨大リファクタを提案しない
- interface 導入、依存反転、責務分割などの具体案を出す

例:

```text
Issue: InappropriateIntimacy
Source: MyApp.Api.UsersController
Target: MyApp.Infrastructure.SqlUserRepository
Location: src/MyApp.Api/UsersController.cs:42

Why it matters:
The API layer directly depends on an infrastructure implementation.
This makes controller tests harder and leaks persistence details upward.

Suggested refactoring:
1. Introduce IUserRepository in MyApp.Application.Abstractions.
2. Inject IUserRepository into UsersController.
3. Register SqlUserRepository in the composition root.
```

---

## 27. SARIF 出力設計

Phase 4 で `--sarif` を追加する。

```bash
dotnet-coupling --sarif --output coupling.sarif ./src
```

目的:

- GitHub code scanning に表示する
- Pull Request 上で設計 issue を見える化する

Mapping:

| dotnet-coupling | SARIF |
|---|---|
| IssueType | ruleId |
| Severity | level |
| Problem | message |
| Location | physicalLocation |
| Recommendation | help text |

実装では Microsoft の `Sarif.Sdk` (`Microsoft.CodeAnalysis.Sarif`) を使う。
`SarifLog`, `Run`, `Tool`, `ToolComponent`, `ReportingDescriptor`, `Result`,
`Location`, `PhysicalLocation`, `ArtifactLocation`, `Region` などの object model を
利用し、独自 SARIF JSON writer は持たない。

Architecture boundary:

- `DotnetCoupling.Sarif` だけが `Sarif.Sdk` に依存する。
- `DotnetCoupling.Core` / `DotnetCoupling.Roslyn` / `DotnetCoupling.Git` は
  `Microsoft.CodeAnalysis.Sarif` に依存しない。
- CLI は composition root として `Json` / `Text` / `Summary` / `Sarif` /
  `Hotspots` を選ぶ。

Suppression:

- `.coupling.json` の `ignore.issues` で suppressed された issue は SARIF には
  出さない。
- summary / JSON には suppressed count と reason を出す。
- suppressed issue は grade と `--check` から除外する。

---

## 28. Complexity-assisted Risk Prioritization

`0.5.0` で `--hotspots` 向けに `cyclomaticComplexity` /
`cognitiveComplexity` を導入する。`0.6.0` 以降では、同じ priority model を
`--impact` / `--trace` / `--ai` などの調査 UX へ広げる。

### 28.1 目的

complexity は coupling health の主判定ではなく、issue の修正優先順位を決める補助指標として使う。

良い使い方:

- high coupling risk かつ high volatility かつ high complexity の箇所を上位に出す
- `--hotspots` の ranking を改善する
- `--impact` で変更リスクの説明を補強する
- `--ai` の修正提案で「なぜここから直すか」を説明する

避けること:

- Balance Score や Grade に直接混ぜる
- complexity だけで issue severity を上げる
- complexity threshold 超過だけで `--check` を失敗させる

### 28.2 Metrics

| Metric | Scope | Purpose |
|---|---|---|
| `cyclomaticComplexity` | method / type aggregate | 分岐数に基づくテスト・変更リスク |
| `cognitiveComplexity` | method / type aggregate | ネストや読みにくさに基づく理解コスト |

type aggregate は、所属 method の max / average / sum を保持する。ranking では
max と sum を優先し、平均値だけで判断しない。

### 28.3 Priority Model

`priorityScore` は issue severity とは別に保持する。

入力候補:

- issue severity
- Balance Score
- volatility
- fan-in / fan-out
- circular dependency participation
- project / package boundary crossing
- cyclomatic complexity
- cognitive complexity

出力例:

```json
{
  "type": "GlobalComplexity",
  "severity": "High",
  "priority": {
    "rank": 1,
    "score": 0.91,
    "reasons": [
      "high coupling risk",
      "high volatility",
      "high cognitive complexity"
    ],
    "cyclomaticComplexity": 18,
    "cognitiveComplexity": 31
  }
}
```

### 28.4 Contract

- JSON schema には optional field として追加する。
- field が無い場合も既存 consumer が動くようにする。
- `--summary` では詳細数値を出しすぎず、hotspot reason として短く表示する。
- complexity は補助指標であり、health grade の denominator には入れない。

---

## 29. Web UI / Visualization

Web UI は Phase 5 の既定タスクから外し、必要性が見えた場合に検討する。

Phase 4 までで PR comment、GitHub Code Scanning、SARIF artifact、Hotspots、
JSON / Markdown 系出力による feedback loop はかなり目的を満たしている。そのため、
当面は常時稼働する server / frontend よりも、CLI と machine-readable output の
安定性を優先する。

再検討条件:

- PR comment / SARIF / Markdown だけでは調査導線が不足する
- 複数 repo / 複数 team の trend を横断して見たい
- 非エンジニアにも設計リスクを継続的に共有したい
- `--impact` / `--trace` / complexity-assisted prioritization が増え、CLI 出力だけでは比較しづらい
- 履歴推移、解消状況、ownership などを可視化する明確な需要がある
