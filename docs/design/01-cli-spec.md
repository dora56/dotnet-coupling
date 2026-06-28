# CLI 仕様・Exit Code

## 5. CLI 仕様

### 5.1 コマンド形式

```text
dotnet-coupling [path] [options]
```

`path` は解析対象のディレクトリ、C# file、`.slnx`、`.sln`、`.csproj` のいずれかを想定する。

Phase 3a では syntax mode のまま `.slnx` / `.sln` / `.csproj` を読み、project name と
project boundary distance を補助する。Phase 3b では明示 `semantic` mode を追加し、
まず `.csproj` / `.sln` を `MSBuildWorkspace` で読み込む preview から始める。

### 5.2 MVP オプション

| オプション | 説明 | 初期値 |
|---|---|---|
| `[path]` | 解析対象パス | `.` |
| `--summary` | サマリのみ表示 | `false` |
| `--json` | JSON 出力 | `false` |
| `--output <file>` | 出力先ファイル | stdout |
| `--check` | 品質ゲートを有効化 | `false` |
| `--min-grade <grade>` | `--check` 時の最低許容グレード | `C` |
| `--fail-on <severity>` | 指定以上の重要度で失敗 | 未指定 |
| `--mode <syntax|semantic>` | 解析モードを指定 | `syntax` |
| `--no-git` | Git 履歴解析をスキップ | `false` |
| `--git-months <n>` | Git 履歴を見る月数 | `6` |
| `--config <file>` | 設定ファイル指定 | 自動探索 |
| `--baseline <ref>` | 指定 Git ref と比較する | 未指定 |
| `--help` | ヘルプ表示 | - |
| `--version` | バージョン表示 | - |

`semantic` は Phase 3b の明示入口として提供する。現時点では preview として
`.csproj` / `.sln` 入力のみを受け付け、summary / JSON には `semantic-preview`
を出力する。その他の path では CLI 引数エラーとして安定した error message を返す。

### 5.3 v0.2 以降のオプション

| オプション | 説明 |
|---|---|
| `--hotspots[=N]` | リファクタリング候補 Top N を表示 |
| `--impact <component>` | 指定コンポーネント変更時の影響範囲を見る |
| `--trace <symbol>` | 指定型・メソッドへの依存を追跡 |
| `--ai` | AI coding agent 向け出力 |
| `--sarif` | GitHub code scanning 向け SARIF 出力 |
| `--jp`, `--japanese` | 日本語説明付き出力 |
| `--web` | Web UI 起動 |

---

## 20. Exit Code 設計

| Exit Code | 意味 |
|---:|---|
| `0` | 正常終了 / check 合格 |
| `1` | check 不合格 |
| `2` | CLI 引数エラー |
| `3` | 解析対象パスエラー |
| `4` | 解析中の予期しないエラー |

MVP では `0` / `1` / `2` だけでもよい。

`--check --baseline <ref>` は ratchet gate として動作する。既存 issue や
総合 grade では失敗させず、baseline ref に存在しない新規 issue が `--fail-on`
以上の severity の場合のみ `1` を返す。`--fail-on` 未指定時の baseline gate は
`High` 以上を失敗条件にする。
