# 将来機能 (Baseline / Hotspots / AI / SARIF)

## 24. Baseline / Ratchet Gate 設計

v0.2 以降で実装する。

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

### 25.1 Hotspots

リファクタリング候補をランキングする。

```bash
dotnet coupling --hotspots=10 ./src
```

スコア要素:

- issue severity
- incoming dependency count
- outgoing dependency count
- volatility
- circular dependency participation
- project boundary crossing

### 25.2 Impact

指定コンポーネントを変更した場合の影響範囲を出す。

```bash
dotnet coupling --impact MyApp.Domain.User ./src
```

出力:

- 直接依存元
- 間接依存元
- risk score
- 影響 project / namespace

### 25.3 Trace

指定 symbol への依存を追う。

```bash
dotnet coupling --trace IUserRepository ./src
```

MVP の syntax-only では精度が出にくいため、semantic mode 後に実装する。

---

## 26. AI 出力設計

v0.2 以降で `--ai` を追加する。

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

v0.3 以降で `--sarif` を追加する。

```bash
dotnet coupling --sarif --output coupling.sarif ./src
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
