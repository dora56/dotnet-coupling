# Phase 3 Self Syntax vs Semantic Compare - 2026-06-28

## Summary

Overall: SEMANTIC_PREVIEW_SELF_DIFF_IS_EXPLAINABLE

`dotnet-coupling.slnx` に対して self compare を rerun した。初期の Phase 3
記録では headline delta がなかったが、追加した semantic shape coverage の後では
`semantic-preview` がより多くの internal coupling を拾い、issue counts にも差分が
出るようになった。

Observed pattern:

- `syntax` / `semantic-preview` ともに grade は `C`
- internal coupling は `507 -> 2566`
- High issues は `8 -> 9`
- Medium issues は `30 -> 55`
- recoverable diagnostics は `0 -> 0`

直前の follow-up では enum member access が `InappropriateIntimacy` として過剰に
増幅されていたが、`Status.Ready` のような enum member access を model coupling と
して扱う修正を入れた結果、semantic high issues は `91 -> 9` まで下がった。

## Environment

- Tool: local build of `DotnetCoupling.Cli.dll`
- Compare script: `scripts/generate-semantic-compare-report.sh`
- Working directory: repository root
- Date: 2026-06-28
- Target: `dotnet-coupling.slnx`

## Latest Compare Snapshot

| Mode | Grade | Internal Couplings | Issues | Diagnostics |
| --- | --- | --- | --- | --- |
| `syntax` | `C` | `507` | `0 Critical, 8 High, 30 Medium` | `0` |
| `semantic-preview` | `C` | `2566` | `0 Critical, 9 High, 55 Medium` | `0` |

## Interpretation

### 1. The remaining self delta is now plausible

After the enum false-positive fix, the self-dogfood delta no longer looks like a
categorical semantic bug. The dominant difference is broader symbol-aware
coupling coverage, not a flood of obviously wrong intrusive findings.

### 2. The CLI/JSON contract remains stable

The semantic run still uses explicit `semantic-preview` mode and keeps the
supported summary / JSON contract intact:

- syntax remains the default mode
- semantic remains opt-in
- no recoverable diagnostics were needed on the self target
- the compare artifact can now explain the metric delta without changing the
  CLI summary contract

### 3. This complements the other Phase 3c evidence

- OSS compare shows the "headline unchanged, coupling expanded" case
- synthetic compare shows the "one explainable semantic-only issue delta" case
- self compare now shows the "larger real-project semantic expansion" case

Together, these three views are enough to explain the current preview behavior
without relying on silent changes.

## Artifact Shape

The generated compare markdown now includes:

- headline diff
- metric diff
- diagnostic diff
- short interpretation guidance

This is the chosen place to explain semantic-only expansion. We do not add new
required fields to the supported CLI summary / JSON contract just to narrate the
compare result.
