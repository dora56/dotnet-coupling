# バージョニング・実装ロードマップ

## 32. バージョニング

Semantic Versioning を採用する。

| Version | 内容 |
|---|---|
| `0.1.0-alpha.1` | syntax-only MVP |
| `0.2.0-alpha.1` | semantic mode / `.slnx` / `.sln` 対応 |
| `0.3.0` | baseline / hotspots / SARIF |
| `0.4.0` | AI output / impact / trace |
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

### Phase 2: Public alpha

- [ ] NuGet.org に `0.1.0-alpha.1` 公開
- [ ] GitHub Releases 作成
- [ ] サンプルプロジェクトで dogfooding
- [ ] `dotnet-coupling` 自身のリポジトリに対する dogfooding を定例化
- [ ] `cargo-coupling` と同等に、継続的 dogfooding を前提に false positive を継続改善
- [ ] false positive を issue 化
- [ ] `--no-git` / Git なし環境の出力改善

### Phase 3: Semantic mode

- [ ] `MSBuildWorkspace` 導入
- [ ] `.slnx` / `.sln` / `.csproj` 対応
- [ ] symbol resolution
- [ ] project boundary distance
- [ ] external package 判定
- [ ] partial class 統合
- [ ] `.coupling.toml` 対応

### Phase 4: CI / team use

- [ ] `--baseline`
- [ ] ratchet gate
- [ ] SARIF 出力
- [ ] GitHub Actions 利用例
- [ ] `--hotspots`

### Phase 5: Advanced UX

- [ ] `--impact`
- [ ] `--trace`
- [ ] `--ai`
- [ ] 日本語出力
- [ ] Web UI
