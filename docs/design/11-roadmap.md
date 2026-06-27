# バージョニング・実装ロードマップ

## 32. バージョニング

Semantic Versioning を採用する。

| Version | 内容 |
|---|---|
| `0.1.0-alpha.1` | syntax-only MVP |
| `0.2.0-alpha.1` | public alpha feedback / config / baseline |
| `0.3.0-alpha.1` | semantic mode / `.slnx` / `.sln` / `.csproj` 対応 |
| `0.4.0` | SARIF / team CI integration / hotspots |
| `0.5.0` | AI output / impact / trace |
| `0.6.0` | complexity-assisted risk prioritization |
| `1.0.0` | CLI と JSON schema を安定化 |

`1.0.0` までは JSON schema の破壊的変更を許容する。ただし変更履歴に明記する。

---

## 33. 実装ロードマップ

### Phase 0: Repository bootstrap

現時点のリポジトリに実コードが存在しない場合、以下は未完了として扱う。スターター zip などが別途ある場合も、リポジトリへ commit されるまでは `[x]` にしない。

- [x] CLI project 作成
- [x] `PackAsTool` 設定
- [x] `dotnet-coupling` コマンド名設定
- [x] `System.CommandLine` 導入
- [x] Roslyn syntax-only 解析の土台
- [x] text / summary / json 出力の土台
- [x] GitHub Actions release 雛形
- [x] GitHub Actions ci 雛形
- [x] husky.Net 導入、設定

### Phase 1: MVP hardening

- [x] CLI parser 整理
- [x] 解析対象ファイル探索の安定化
- [x] generated code 除外
- [x] `Visibility` / `UsageContext` / `Distance` の単体テスト
- [x] Balance Score の clamp 境界値テスト
- [x] issue density ベース Grade の単体テスト
- [x] Property-Based Testing 追加
- [x] Hidden Coupling / temporal co-change の MVP 実装
- [x] Scattered External Coupling の MVP 実装
- [x] issue 検出ロジックの単体テスト
- [x] Mutation Testing 追加
- [x] JSON schema 固定
- [x] manifest / blind spots 出力
- [x] README 整備
- [x] NuGet metadata 整備
- [x] ローカル tool install 検証
- [x] GitHub Release ノート生成
- [x] ローカル pre-commit ガード
- [x] Analyzer 責務分割
- [x] fixture / golden regression tests
- [x] CLI / JSON schema / renderer regression tests
- [x] Mutation score 60% gate
- [x] Coverage 収集を補助指標として導入
- [x] mutation / coverage report artifact 出力
- [x] CI cache / locked restore / `packages.lock.json`

### Phase 1 retrospective

Phase 1 では、当初 Phase 2 以降でもよかった品質基盤を前倒しで整備した。
特に mutation testing、coverage、golden/schema tests、CI artifact、locked restore
まで入ったため、Phase 2 は機能追加よりも **public alpha と dogfooding で実利用の
フィードバックを集めるフェーズ** に寄せる。

設計上の含意:

- 単一 CLI project は維持するが、内部 module 境界は v0.3 の multi-project split
  に備えて明文化済み。
- `FsCheck.Xunit` は xUnit v3 と噛み合わせが悪いため使わない。`FsCheck` core を
  xUnit v3 `[Fact]` から直接呼び、scoring の不変条件を検証する。
- Mutation score を主指標、coverage を補助指標とする。coverage threshold gate は
  Phase 1/2 では導入しない。
- JSON schema と CLI exit code は Phase 2 dogfooding 中も意図なく壊さない。

### Phase 2: Public alpha

Goal: `0.1.0-alpha.1` を外へ出し、syntax-only MVP の false positive / false
negative を実利用で把握する。

Release readiness:

- [ ] GitHub Actions を remote で実走し、cache / locked restore / artifacts を確認
- [ ] `dotnet pack` artifact を CI で保存
- [ ] local tool install smoke を CI に追加
- [ ] GitHub Releases 作成
- [ ] NuGet.org に `0.1.0-alpha.1` 公開
- [ ] Release notes に schema version / blind spots / syntax-only limitation を明記

Dogfooding:

- [ ] `dotnet-coupling` 自身のリポジトリに対する dogfooding を定例化
- [ ] 2-3 個の小規模 OSS / サンプルプロジェクトで dogfooding
- [ ] false positive / false negative を issue 化
- [ ] `--no-git` / Git なし環境の出力改善
- [ ] System / Microsoft / internal namespace 除外ルールの実例検証
- [ ] Hidden Coupling の commit size / threshold の実例検証

Configuration and alpha feedback:

- [ ] `.coupling.json` MVP 対応
- [ ] threshold 設定: fan-in/out, scattered external breadth, temporal coupling
- [ ] ignore 設定: path / namespace / issue type
- [ ] `--config` の実装
- [ ] config schema / sample config

Baseline and team gate:

- [ ] `--baseline <ref>` MVP
- [ ] new / resolved / unchanged issues の分類
- [ ] ratchet gate: new High 以上のみ fail
- [ ] GitHub Actions 利用例

### Phase 3: Semantic mode and project model

Goal: syntax-only の限界を越え、project / assembly / package 境界を正確に扱う。

Architecture:

- [ ] `DotnetCoupling.Core` / `DotnetCoupling.Roslyn` / `DotnetCoupling.Git` への分割判断
- [ ] structural fitness test: CLI から Roslyn / issue policy への依存方向を固定
- [ ] `Models` の public API 境界整理

Semantic analysis:

- [ ] `MSBuildWorkspace` 導入
- [ ] `.slnx` / `.sln` / `.csproj` 対応
- [ ] symbol resolution
- [ ] project boundary distance
- [ ] assembly / NuGet package 判定
- [ ] partial class 統合
- [ ] alias / global using 解決
- [ ] attribute / invocation / member access の精度向上

Performance:

- [ ] syntax mode と semantic mode の明示的な mode 分離
- [ ] workspace load diagnostics
- [ ] 大規模 repo の perf baseline

### Phase 4: CI / team use

Goal: チーム開発の PR feedback loop に載せる。

- [ ] SARIF 出力
- [ ] GitHub code scanning integration
- [ ] `--hotspots`
- [ ] issue suppression / baseline update workflow
- [ ] CI example templates
- [ ] report artifact examples
- [ ] JSON schema changelog

### Phase 5: Advanced UX

Goal: 調査・修正計画に使える体験へ広げる。

- [ ] `--impact`
- [ ] `--trace`
- [ ] `--ai`
- [ ] Markdown report
- [ ] 日本語出力
- [ ] Web UI

### Phase 6: Complexity-assisted prioritization

Goal: coupling issue の「良し悪し」ではなく、**どれから直すべきか** の優先順位を
補助する。

Principles:

- [ ] `cyclomaticComplexity` / `cognitiveComplexity` は Balance Score / Grade の主計算に入れない
- [ ] issue severity を直接上げる材料にしない
- [ ] `--hotspots` / `--impact` / `--ai` の ranking 補助として使う
- [ ] 高結合 + 高変更頻度 + 高複雑度の交点を優先する
- [ ] JSON schema には optional field として追加し、後方互換を保つ

Implementation:

- [ ] syntax mode で method / type 単位の cyclomatic complexity を計算
- [ ] semantic mode で symbol と complexity metric を安定して紐づける
- [ ] cognitive complexity のルールセットを明文化
- [ ] `priorityScore` / `riskPriority` を issue とは別概念として追加
- [ ] priority reasons を出力: high coupling, high volatility, high complexity など
- [ ] threshold / weight を config で調整可能にする
- [ ] complexity metric の fixture / golden / property tests を追加
- [ ] complexity が高いだけでは fail しないことを CLI contract に明記
