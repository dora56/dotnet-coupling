# エンジニアリング (パフォーマンス・セキュリティ・テスト・CI)

## 28. パフォーマンス設計

### 28.1 MVP 方針

- syntax tree 解析はファイル単位で並列化する
- `bin`, `obj`, `.git` は必ず除外する
- 大きい generated file は除外する
- Git 履歴解析は1回だけ実行し、file path -> count の dictionary にする

### 28.2 目標

| 規模 | 目標 |
|---|---|
| 100 files | 1秒以内 |
| 1,000 files | 5秒以内 |
| 5,000 files | 30秒以内 |

semantic mode は遅くなるため、将来的に以下のモードを分ける。

```bash
dotnet coupling --mode syntax ./src
dotnet coupling --mode semantic ./MyApp.sln
```

### 28.3 キャッシュ

v0.3 以降で検討。

```text
.coupling/cache/
  file-hash -> parsed facts
  git-revision -> volatility facts
```

---

## 29. セキュリティ / プライバシー

### 29.1 基本方針

- ソースコードを外部送信しない
- ネットワーク通信しない
- 解析対象ファイルの読み取りのみ行う
- `git` 実行以外の外部プロセスを起動しない
- デフォルトではレポートを stdout にのみ出す

### 29.2 Git 実行の安全性

`git log` は外部プロセスなので、shell 経由で実行しない。`ProcessStartInfo.ArgumentList` を使い、ユーザー入力や pathspec を個別引数として渡す。

悪い例:

```csharp
startInfo.Arguments = $"log --since=\"{months} months ago\" -- {path}";
```

良い例:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = "git",
    WorkingDirectory = repoRoot,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false
};

startInfo.ArgumentList.Add("log");
startInfo.ArgumentList.Add("--pretty=format:");
startInfo.ArgumentList.Add("--name-only");
startInfo.ArgumentList.Add("--diff-filter=AMRC");
startInfo.ArgumentList.Add($"--since={months} months ago");
startInfo.ArgumentList.Add("--");
startInfo.ArgumentList.Add("*.cs");
```

追加方針:

- `repoRoot` は実在する directory に正規化する
- `git` 以外のコマンドは実行しない
- `PATH` 上の `git` を使うが、将来 `--git-path` を追加する場合も validation する
- stderr は verbose mode 以外では必要以上に表示しない

### 29.3 .NET tool の信頼性

.NET tool はユーザー環境で full trust で実行される。利用者に信頼されるため、以下を整備する。

- public repository
- signed tags / releases
- Release notes
- SBOM
- NuGet package metadata
- 依存 package の更新方針

### 29.4 CI Secret

NuGet 公開時の `NUGET_API_KEY` は GitHub Actions secret として管理する。

---

## 30. テスト設計

### 30.0 テスト戦略モデル

**Strategy: `pyramid`**

CLI ツール / 分析エンジンであり UI 層がないため、Testing Trophy ではなく pyramid を採用する。

```text
        /\
       /E2\        ← dotnet tool install → dotnet-coupling --check (few)
      /----\
     / Integ\      ← Golden file tests, fixture project 解析 (moderate)
    /--------\
   /   Unit   \    ← FsCheck + xUnit [Theory] (many, fast)
  /____________\
  [  Static   ]    ← Nullable / format / analyzers (always, PR gate)
```

| Layer | Size | 内容 |
|---|---|---|
| Static | — | `<Nullable>enable</Nullable>` + `dotnet format --verify-no-changes` |
| Unit | Small | FsCheck property + xUnit Theory/InlineData |
| Integration | Medium | Fixture project 解析 + golden file 比較 |
| E2E | Large | `dotnet tool install` → 実行 → uninstall |

### 30.1 Unit Tests

| 対象 | テスト内容 |
|---|---|
| Strength classifier | syntax pattern -> IntegrationStrength |
| Distance calculator | namespace / project path -> Distance |
| Volatility classifier | change count -> Volatility |
| Balance scoring | score formula の境界値 |
| Issue detector | known graph -> expected issues |
| Report renderer | golden file comparison |
| Config loader | MVP は JSON 読み込み、v0.2 で TOML 追加 |

### 30.2 Fixture Projects

```text
tests/fixtures/
  simple-balanced/
  global-complexity/
  circular-dependency/
  high-afferent/
  high-efferent/
  concrete-leak/
  generated-code/
  partial-class/
```

### 30.3 Integration Tests

- `dotnet run -- ./fixtures/simple-balanced`
- `dotnet pack`
- local `dotnet tool install --add-source`
- `dotnet-coupling --json`
- `dotnet-coupling --check`

### 30.4 Golden Tests

CLI 出力の安定性を見るため、期待出力をファイル化する。

```text
tests/golden/simple-summary.txt
tests/golden/global-complexity.json
```

JSON は property order に依存しない比較にする。

### 30.5 Property-Based Testing (FsCheck)

Balance Score 公式・Classifier・Grade 判定には数学的性質があるため、FsCheck で全入力空間を検証する。

#### パッケージ

```xml
<PackageReference Include="FsCheck" Version="3.*" />
<PackageReference Include="FsCheck.Xunit" Version="3.*" />
```

#### 検証すべき Property

| Property | 対象 | 内容 |
|---|---|---|
| Bounded output | `BalanceScore` | 任意の `(strength, distance, volatility) ∈ [0,1]³` に対し `score ∈ [0,1]` |
| Monotonicity | Volatility impact | `volatility` 増加 → score は非増加（strength 固定時） |
| Symmetry | Alignment | `strength = 1 - distance` のとき alignment が最大 |
| Grade stability | Grade | score 微小変動（±ε）で Grade が2段階以上ジャンプしない |
| Idempotency | Classifier | 同一入力に対して同一 `IntegrationStrength` を返す |
| Round-trip | Git log parser | `serialize(parse(raw)) ≈ normalized(raw)` |
| Clamp invariant | 全 `Math.Clamp` 呼び出し | 出力が常に `[min, max]` 内 |

#### コード例

```csharp
[Property]
public Property BalanceScore_AlwaysBounded(
    NormalFloat strength, NormalFloat distance, NormalFloat volatility)
{
    var s = Math.Clamp((double)strength, 0.0, 1.0);
    var d = Math.Clamp((double)distance, 0.0, 1.0);
    var v = Math.Clamp((double)volatility, 0.0, 1.0);

    var score = BalanceScoring.Calculate(s, d, v);

    return (score >= 0.0 && score <= 1.0).ToProperty();
}

[Property]
public Property HigherVolatility_LowerOrEqualScore(
    NormalFloat strength, NormalFloat distance,
    NormalFloat vol1, NormalFloat vol2)
{
    var s = Math.Clamp((double)strength, 0.0, 1.0);
    var d = Math.Clamp((double)distance, 0.0, 1.0);
    var v1 = Math.Clamp((double)vol1, 0.0, 1.0);
    var v2 = Math.Clamp((double)vol2, 0.0, 1.0);

    var (lo, hi) = v1 <= v2 ? (v1, v2) : (v2, v1);

    var scoreLo = BalanceScoring.Calculate(s, d, lo);
    var scoreHi = BalanceScoring.Calculate(s, d, hi);

    return (scoreLo >= scoreHi).ToProperty();
}
```

#### 運用方針

- `Arbitrary<T>` でカスタムジェネレータを定義し `[0.0, 1.0]` の正規化済み値を生成
- Seed を固定して CI で再現可能にする（flaky にしない）
- CI では `MaxTest = 500`、ローカルでは `1000`

### 30.6 Mutation Testing (Stryker.NET)

テストの品質（kill 率）を検証するため [Stryker.NET](https://github.com/stryker-mutator/stryker-net) を使用する。

Coverage は補助指標として扱う。line / branch coverage は「未実行領域を見つける」
ために使い、テスト品質の主判定は mutation score に置く。Coverage だけを上げる
ための浅いテスト追加は避け、coverage で見つかった穴は Stryker の生存 mutant や
境界条件テストと突き合わせて優先順位を決める。

```bash
dotnet test --configuration Release \
  --settings coverage.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults/Coverage
```

Phase 1 の coverage 設定は `coverage.runsettings` に置き、Cobertura XML を出力する。
対象は `DotnetCoupling.Core` assembly の scoring / issue detection に絞り、
CLI entrypoint の `Program.cs` と test assembly は除外する。

CI では mutation testing を独立した必須 job として実行し、`stryker-config.json`
の `break` threshold で失敗させる。Coverage は同じ CI 内で収集するが、Phase 1
では threshold gate にしない。CI は `coverage-report` と `mutation-report` を
artifact として保存し、coverage は Cobertura XML、mutation は Stryker HTML/JSON
report を確認できるようにする。

#### 対象と除外

| 対象（mutate する） | 除外（mutate しない） |
|---|---|
| `DotnetCoupling.Core` の scoring / classifier / issue detection | `Program.cs`（CLI entrypoint） |
| Balance Score 計算 | `ReportRenderer.cs`（出力フォーマット） |
| Grade 判定ロジック | Git プロセス起動部分 |
| Circular dependency 検出 | テストコード自体 |

#### 設定

```json
{
  "stryker-config": {
    "project": "src/DotnetCoupling.Core/DotnetCoupling.Core.csproj",
    "test-projects": ["tests/DotnetCoupling.Tests/DotnetCoupling.Tests.csproj"],
    "mutate": [
      "CouplingScoring.cs",
      "IssueDetector.cs"
    ],
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 60
    },
    "reporters": ["html", "json", "progress"],
    "concurrency": 4,
    "mutation-level": "Standard",
    "ignore-mutations": [
      "assignment",
      "initializer",
      "block",
      "statement",
      "unary",
      "update",
      "string",
      "stringmethod",
      "linq",
      "math",
      "checked",
      "regex",
      "bitwise"
    ],
    "test-runner": "mtp"
  }
}
```

`mtp` を使うのは、現在のテスト構成では coverage-based optimization が安定して効き、
`vstest` より mutation 対象数と実行時間を抑えやすいためである。
また Phase 3 時点では、score / grade / issue 境界を守る回帰検知を優先し、
`arithmetic` / `logical` / `equality` / `boolean` を主対象として残す。

#### 特に有効なミューテーション

| ミューテーション種類 | 検出対象コード |
|---|---|
| 算術演算子置換 (`+` → `-`) | Balance Score の `Math.Abs`, `Math.Clamp` |
| 比較演算子置換 (`>=` → `>`) | Grade 判定の閾値境界 |
| 論理結合置換 (`&&` → `\|\|`) | Issue 検出の条件式 |
| 定数変更 | Volatility 閾値 (`3..10` → `Medium`) |

#### 閾値

| 指標 | 値 | 意味 |
|---|---|---|
| `high` | 80% | この以上なら十分 |
| `low` | 60% | この以下は要改善 |
| `break` | 60% | CI を失敗させる最低ライン |

#### CI 配置

| Suite | Stryker 実行 | 理由 |
|---|---|---|
| PR gate | Yes (`since`) | 変更差分だけを mutation して feedback loop を維持する |
| Main branch | Yes (full) | merge 後の回帰を full scope で検知する |
| Nightly | Optional | 長期 trend や heavy な構成に広げる場合に使う |
| Release gate | No | 公開フローの責務を pack/publish/release に限定する |

### 30.7 テスト依存パッケージ

```xml
<!-- tests/DotnetCoupling.Tests/DotnetCoupling.Tests.csproj -->
<ItemGroup>
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  <PackageReference Include="FsCheck" Version="3.*" />
  <PackageReference Include="FsCheck.Xunit" Version="3.*" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
</ItemGroup>
```

Global tool:

```bash
dotnet tool install --global dotnet-stryker
```

### 30.8 CI Suite 設計

| Suite | Trigger | 含むテスト | 目標時間 |
|---|---|---|---|
| PR gate | `pull_request` | Static + Unit (small) + Integration (medium) + diff-scoped mutation | < 5 min |
| Main push | `main` への push | Static + Unit + Integration + full mutation | < 15 min |
| Nightly | schedule | Optional: full mutation + extended diagnostics | < 20 min |
| Release | tag push | All + E2E + pack/publish/release | < 10 min |

PR の mutation job では Stryker.NET の `since` を使い、`pull_request.base.sha`
以降の差分に限定して実行する。`main` への push では full mutation を実行し、
公開前の最終 mutation gate は release workflow ではなく通常 CI に置く。

---

## 31. GitHub Actions 設計

### 31.1 CI

`TargetFramework` は `net10.0` とする。MVP では .NET 8 / 9 互換を検証しない。CI は SDK `10.0.x` を使い、将来 .NET 11 が安定した時点で追加 matrix を検討する。

```yaml
name: ci

on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-build
```

### 31.2 Release

Release も SDK `10.0.x` で実行し、package target は `net10.0` とする。

```yaml
name: release

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet test --configuration Release
      - run: dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj --configuration Release --no-build
      - run: dotnet nuget push src/DotnetCoupling.Cli/nupkg/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

### 31.3 Nightly (Mutation Testing)

Stryker.NET による mutation score の regression 検知を nightly で実行する。

```yaml
name: nightly-mutation

on:
  schedule:
    - cron: '0 3 * * *'  # JST 12:00
  workflow_dispatch:

jobs:
  mutation:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool install --global dotnet-stryker
      - run: dotnet restore
      - run: dotnet stryker --config-file stryker-config.json
      - uses: actions/upload-artifact@v4
        with:
          name: stryker-report
          path: StrykerOutput/**/reports/
```

### 31.4 GitHub Release ノート生成 (`softprops/action-gh-release`)

NuGet への publish とは別に、GitHub Releases を自動作成して配布情報と変更履歴を残す場合は
[`softprops/action-gh-release`](https://github.com/softprops/action-gh-release) を採用できる。

採用方針:

- トリガーは `v*.*.*` tag push（NuGet publish と同じ）
- NuGet publish 後に実行し、release note を生成する
- `generate_release_notes: true` を基本とし、必要に応じて `body` を追記する
- 権限は最小化し、`contents: write` のみを付与する

```yaml
name: release

on:
  push:
    tags:
      - 'v*.*.*'

permissions:
  contents: write

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet test --configuration Release
      - run: dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj --configuration Release --no-build
      - run: dotnet nuget push src/DotnetCoupling.Cli/nupkg/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}

  github_release:
    needs: publish
    runs-on: ubuntu-latest
    steps:
      - uses: softprops/action-gh-release@v2
        with:
          generate_release_notes: true
          draft: false
          prerelease: false
```

補足:

- 既存の release ワークフローに段階追加する場合は `github_release` job を追加する
- 単一 job で行う場合も `dotnet nuget push` 成功後に `action-gh-release` を実行する

### 31.5 ローカル pre-commit ガード (`Husky.Net`)

CI だけでなく、ローカルの commit 前にも最低限の品質ゲートを置く場合は [`Husky.Net`](https://github.com/alirezanet/Husky.Net) を使える。
この用途では、pre-commit で staged file の `dotnet format` を実行し、commit-msg で Conventional Commits を検証する構成が最も自然である。

採用方針:

- pre-commit で staged された `.cs` / `.csproj` などだけを対象に `dotnet format --verify-no-changes` を実行する
- commit-msg で commit message が Conventional Commits に従うかを検証する
- hook の失敗時は commit を中断し、ローカルで即時に修正できるようにする
- hook は補助線とし、最終的な正は CI の `dotnet format` / build / test とする

```bash
# pre-commit
staged_files=$(git diff --cached --name-only --diff-filter=ACMR -- '*.cs' '*.csproj' '*.slnx' '*.sln')
if [ -n "$staged_files" ]; then
  dotnet format --verify-no-changes --include $staged_files
fi

# commit-msg
commit_message_file=$1
commit_message=$(cat "$commit_message_file")

# Conventional Commits の例: feat:, fix:, docs:, refactor:, test:, chore:
if ! echo "$commit_message" | grep -Eq '^(feat|fix|docs|refactor|test|chore)(\([^)]+\))?: .+'; then
  echo "Commit message must follow Conventional Commits"
  exit 1
fi
```

補足:

- `dotnet format` は staged file のみを対象にすることで、未コミットの関係ない変更を巻き込まない
- Conventional Commits の検証は pre-commit ではなく commit-msg に置くのが適切である
- ルールが厳しすぎる場合は、local では warning、CI で hard fail に分ける運用も可能である
