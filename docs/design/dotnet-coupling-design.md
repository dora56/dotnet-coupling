# dotnet-coupling 設計書

- 作成日: 2026-06-27
- レビュー反映日: 2026-06-27
- ステータス: Draft / cargo-coupling v0.3.3 照合レビュー反映 + net10.0 baseline 版
- 対象: `cargo-coupling` の .NET / C# 版 CLI ツール
- 配布形態: .NET tool / NuGet package
- 想定コマンド名: `dotnet-coupling`

---

## ファイル構成

本設計書は関心ごとに分割されています。必要な部分だけ読んでください。

| ファイル | 内容 | 元セクション |
|---|---|---|
| [00-overview.md](00-overview.md) | 目的・ゴール・想定ユーザー・利用イメージ | §1-4 |
| [01-cli-spec.md](01-cli-spec.md) | CLI オプション・Exit Code | §5, §20 |
| [02-distribution.md](02-distribution.md) | NuGet 配布・csproj・パッケージ設計 | §6 |
| [03-architecture.md](03-architecture.md) | アーキテクチャ・パイプライン・解析粒度・Roslyn | §7-10 |
| [04-data-model.md](04-data-model.md) | データモデル (Component, Metrics, Issue) | §11 |
| [05-scoring.md](05-scoring.md) | Strength / Distance / Volatility / Balance Score / Grade | §12-16 |
| [06-issue-detection.md](06-issue-detection.md) | Issue 検出・Severity・循環依存 | §17-18, §23 |
| [07-output-formats.md](07-output-formats.md) | レポート出力 (Text / Summary / JSON) | §19 |
| [08-config.md](08-config.md) | 設定ファイル・外部依存の扱い | §21-22 |
| [09-future-features.md](09-future-features.md) | Baseline / Hotspots / AI / SARIF | §24-27 |
| [10-engineering.md](10-engineering.md) | パフォーマンス・セキュリティ・テスト・CI | §28-31 |
| [11-roadmap.md](11-roadmap.md) | バージョニング・実装ロードマップ | §32-33 |
| [12-reference.md](12-reference.md) | 元ツール対応表・Blind Spots・リスク・原則・レビュー履歴 | §34-41 |

### 読む順序の目安

- **実装を始めるとき**: 00 → 03 → 04 → 05
- **スコアリングルールを確認**: 05 → 06
- **出力形式を決めるとき**: 07 → 01
- **CI/配布を設定するとき**: 02 → 10
- **全体像を掴みたいとき**: 00 → 11


---

> 全内容は上記の分割ファイルに移動しました。
> 更新は分割ファイル側で行い、このファイルはインデックスとして維持します。
