# CLI 仕様・Exit Code

## 5. CLI 仕様

### 5.1 コマンド形式

```text
dotnet-coupling [path] [options]
```

`path` は解析対象のディレクトリ、`.sln`、`.csproj` のいずれかを想定する。

MVP ではディレクトリ解析を主対象とし、`.sln` / `.csproj` は v0.2 の semantic analysis で正式対応する。

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
| `--no-git` | Git 履歴解析をスキップ | `false` |
| `--git-months <n>` | Git 履歴を見る月数 | `6` |
| `--config <file>` | 設定ファイル指定 | 自動探索 |
| `--help` | ヘルプ表示 | - |
| `--version` | バージョン表示 | - |

### 5.3 v0.2 以降のオプション

| オプション | 説明 |
|---|---|
| `--baseline <ref>` | 指定 Git ref と比較する |
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
