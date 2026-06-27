# 概要・目的・想定ユーザー

## 1. 目的

`dotnet-coupling` は、C# / .NET プロジェクトの結合度を静的解析し、設計上のリスクを CLI で可視化するツールである。

元になる思想は `cargo-coupling` と同じく、単純に「依存が多い = 悪い」とは見なさない。結合を以下の3軸で評価し、**結合の強さ・距離・変化しやすさのバランス**を見る。

1. **Integration Strength**: 依存の強さ
2. **Distance**: 依存先との距離
3. **Volatility**: 依存先の変更頻度

この3軸から Balance Score を算出し、以下のような問いに答える。

- 遠い場所にある具象クラスへ強く依存していないか
- 変更頻度の高い領域へ、強い依存を向けていないか
- 依存が集中しすぎている型・名前空間・プロジェクトはないか
- CI で新しい設計劣化を検知できるか
- リファクタリングの優先順位を決められるか

最初から完璧なアーキテクチャ診断ツールを目指すと、実装が沼になる。MVP では **C# の構文解析 + Git 履歴 + CLI 出力 + dotnet tool 公開** に集中する。

---

## 2. ゴールと非ゴール

### 2.1 ゴール

MVP のゴールは以下とする。

- `dotnet tool install --global dotnet-coupling` でインストールできる
- `dotnet-coupling ./src` または `dotnet coupling ./src` で実行できる
- C# ファイルを解析して型レベルの依存グラフを作る
- Strength / Distance / Volatility から Balance Score を出す
- テキスト、サマリ、JSON を出力できる
- `--check` によって CI で失敗判定できる
- NuGet.org に公開できるパッケージ構成にする

### 2.2 非ゴール

MVP では以下をやらない。

- 完全な semantic analysis
- DI コンテナの実行時解決
- Web UI
- Visual Studio 拡張
- IDE Analyzer としてのリアルタイム警告
- すべての C# 言語機能への完全対応
- F# / VB.NET 対応

これらは v0.2 以降の拡張対象とする。

---

## 3. 想定ユーザー

| ユーザー | 目的 |
|---|---|
| 個人開発者 | 自分の C# プロジェクトの結合リスクをざっくり把握する |
| チームリード | リファクタリング対象を見つける |
| アーキテクト | レイヤー違反や設計劣化の兆候を見る |
| CI 管理者 | 新規の高リスク結合を Pull Request で止める |
| AI coding agent 利用者 | AI に渡すための設計診断コンテキストを得る |

---

## 4. 利用イメージ

### 4.1 インストール

```bash
dotnet tool install --global dotnet-coupling
```

### 4.2 基本実行

```bash
dotnet-coupling ./src
```

`ToolCommandName` を `dotnet-coupling` にするため、環境によっては以下も使える。

```bash
dotnet coupling ./src
```

### 4.3 サマリ出力

```bash
dotnet coupling --summary ./src
```

### 4.4 JSON 出力

```bash
dotnet coupling --json ./src > coupling-report.json
```

### 4.5 CI ゲート

```bash
dotnet coupling --check --min-grade B ./src
```

### 4.6 Git 履歴を無視

```bash
dotnet coupling --no-git ./src
```

### 4.7 レポートファイル出力

```bash
dotnet coupling --output report.md ./src
```
