# リファレンス (対応表・Blind Spots・リスク・原則)

## 34. 元ツールとの対応表

| cargo-coupling | dotnet-coupling |
|---|---|
| Rust AST `syn` | Roslyn `Microsoft.CodeAnalysis.CSharp` |
| crate | project / assembly |
| module | namespace / type |
| trait | interface |
| struct / enum | class / record / struct / enum |
| `pub`, `pub(crate)` | `public`, `internal`, `protected`, `private` |
| `cargo coupling` | `dotnet coupling` |
| `.coupling.toml` | MVP は `.coupling.json`、v0.2 で `.coupling.toml` |
| `--json` | `--json` |
| `--check` | `--check` |
| `--baseline` | `--baseline` |
| `--hotspots` | `--hotspots` |
| `--impact` | `--impact` |
| `--trace` | `--trace` |
| `--web` | `--web`, future |

---

## 35. Blind Spots

ツールが明示的に「見ない」または「MVP では弱い」領域を出力に含める。

MVP の blind spots:

- DI container の実行時解決
- reflection
- `dynamic`
- source generator 生成コード
- multi-targeting の条件差分
- `#if` による conditional compilation
- NuGet package 内部の依存
- DB schema / message queue / HTTP API などコード外の結合
- 組織的距離、チーム境界、デプロイ境界

これは重要。静的解析ツールは「観測できたもの」しか言えない。何も出ないことは「問題がない」ではなく、「観測された問題がない」である。

---

## 36. リスクと対策

| リスク | 内容 | 対策 |
|---|---|---|
| 誤検知が多い | syntax-only では型解決が曖昧 | confidence 表示、semantic mode を追加 |
| 遅い | 大規模 solution で解析時間が長い | 並列化、除外設定、syntax mode |
| NuGet 名称衝突 | `dotnet-coupling` が既に使われている可能性 | 公開前に NuGet.org で確認 |
| JSON schema 変更 | CI 利用者に影響 | 1.0 まで experimental 明記 |
| 設計思想が伝わらない | 単なる依存数ツールに見える | README と出力で Strength/Distance/Volatility を説明 |
| Web UI に早く行きすぎる | CLI が固まらない | CLI / JSON を先に安定化 |

---

## 37. 最初の実装で守ること

1. **MVP を小さく保つ**
2. **JSON schema を早めに決める**
3. **false positive を恐れすぎない**
4. **ただし出力には confidence を出す**
5. **Git なし環境でも動く**
6. **NuGet 公開を最初から意識する**
7. **Web UI は後回し**
8. **semantic mode は後回しだが、設計上は逃げ道を作る**

---

## 38. 推奨 README 構成

```text
# dotnet-coupling

Measure the right distance in your .NET code.

## Install
## Quick Start
## What it measures
  - Strength
  - Distance
  - Volatility
## CLI Options
## JSON Output
## CI Usage
## Configuration
## Known Limitations
## Roadmap
## Contributing
## License
```

---

## 39. レビュー反映メモ

`cargo-coupling` v0.3.3 との照合レビューを受け、以下を修正した。

| 指摘 | 対応 |
|---|---|
| Grade が平均スコア + penalty 方式になっていた | issue density ベースに変更 |
| S grade が Excellent 扱いだった | 過剰最適化の警告へ変更 |
| `SameType = 0.00` が score を歪める | SameType は解析対象から除外、最小距離を `SameNamespace = 0.25` に変更 |
| .NET 対象バージョン | MVP から `net10.0` を最低対象にし、`<RollForward>Major</RollForward>` で .NET 11+ 実行を許容 |
| IssueType が元ツールから乖離 | MVP は `cargo-coupling` v0.3.3 aligned core を優先 |
| Balance Score の clamp 未記載 | `Math.Clamp` 必須に変更 |
| `Visibility` 未定義 | C# 固有 enum を追加 |
| `UsageContext` 未定義 | enum と strength mapping を追加 |
| TOML parser 未選定 | MVP は JSON のみ、TOML は v0.2 + Tomlyn 候補 |
| Git command injection 懸念 | `ProcessStartInfo.ArgumentList` 使用を明記 |
| generated code 除外不足 | `.generated.cs`, `.AssemblyInfo.cs`, `GlobalUsings.g.cs`, `.vs` を追加 |
| namespace-level circular dependency の外部 edge | `System.*`, `Microsoft.*`, 外部 namespace 除外を明記 |
| JSON schema 安定性 | `$schema`, `schemaVersion`, `manifest` を追加 |
| CLI parser 未選定 | `System.CommandLine` を採用 |
| Phase 0 が実態と不一致 | リポジトリに commit されるまで未完了扱いに変更 |
| Grade 判定順序の B 重複 | `F -> D -> C -> S -> A -> B` に整理し、B は fallback のみに変更 |
| S grade の `--check` 順序 | `S >= A > B > C > D > F` と明記 |
| `BaseType` の抽象/具象分岐 | abstract base は Contract 既定、concrete base は Functional に変更 |
| Hidden Coupling の計算方法 | commit marker 付き `git log` と co-change pair 集計を追記 |
| syntax-only Distance 推定 | namespace prefix / Component Index によるヒューリスティックを追記 |
| 外部 coupling issue の扱い | `issues` と `--check` には含めるが、health grade density 分母からは除外と明記 |

---

## 40. 参考資料

- cargo-coupling repository: https://github.com/nwiizo/cargo-coupling
- .NET tools overview: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools
- Create a .NET tool: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create
- Publish NuGet packages: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- Roslyn SDK: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/

---

## 41. 結論

`dotnet-coupling` は、まず **Roslyn syntax-only analyzer + Git volatility + CLI report + NuGet dotnet tool** として出す。

最初の勝ち筋は、以下が成立する状態である。

```bash
dotnet tool install --global dotnet-coupling
dotnet coupling ./src
dotnet coupling --check --min-grade B ./src
```

この状態まで持っていけば、あとは実プロジェクトに当てて false positive を潰しながら育てられる。設計ツールは机上で完成させるより、実コードにぶつけて鍛える方が強い。
