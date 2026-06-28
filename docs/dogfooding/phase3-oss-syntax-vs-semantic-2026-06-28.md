# Phase 3 OSS Syntax vs Semantic Compare - 2026-06-28

## Summary

Overall: SEMANTIC_PREVIEW_WORKS_WITH_NOTED_LIMITATIONS

Phase 2 で使った 3 件の OSS リポジトリに対して、Phase 3 の `syntax` /
`semantic-preview` compare を再実行した。3 件とも report generation には成功し、
Phase 2 で見えていた duplicate type-key crash は再現しなかった。

Observed pattern:

- 3 件とも **grade は不変**
- 3 件とも **issue counts も不変**
- ただし **internal coupling count は大きく増える**
- `commandlineparser/commandline` では .NET Framework targeting pack 不足に起因する
  recoverable diagnostics が `semantic-preview` に 6 件出た

これは、現時点の `semantic-preview` が **headline result を壊さずに symbol-aware な
coupling expansion を始められている** 一方で、Phase 3c の gate としては
`coupling count increase の説明責務` と `workspace diagnostics の扱い` を
もう少し整える必要があることを示している。

## Environment

- Tool: locally packed `dotnet-coupling 0.2.0-alpha.1`
- Compare script: `scripts/generate-semantic-compare-report.sh`
- Working directory: `/private/tmp/dotnet-coupling-phase3-oss-compare`
- Date: 2026-06-28

## Targets

| Repository | Commit | Target | Notes |
| --- | --- | --- | --- |
| `ardalis/GuardClauses` | `41162c46946214600a1f5a55b0abc94b0744691a` | `src/GuardClauses/GuardClauses.csproj` | clean compare |
| `altmann/FluentResults` | `36284d3633abc3dbf8e165f665cff754c5bfceb7` | `src/FluentResults/FluentResults.csproj` | clean compare |
| `commandlineparser/commandline` | `1e3607b97af6141743edb3c434c06d5b492f6fb3` | `src/CommandLine/CommandLine.csproj` | semantic diagnostics present |

## Results

| Repository | Syntax | Semantic Preview | Headline Delta |
| --- | --- | --- | --- |
| `GuardClauses` | `Grade S`, `73 internal`, `0 High`, `0 Medium` | `Grade S`, `221 internal`, `0 High`, `0 Medium` | coupling only |
| `FluentResults` | `Grade S`, `82 internal`, `0 High`, `0 Medium` | `Grade S`, `328 internal`, `0 High`, `0 Medium` | coupling only |
| `commandline` | `Grade C`, `502 internal`, `1 High`, `30 Medium` | `Grade C`, `2005 internal`, `1 High`, `30 Medium`, `6 diagnostics` | coupling + diagnostics |

## Findings

### 1. Duplicate type-key crash no longer reproduces

The original Phase 2 blocker did not recur on any of the three repositories.
That gives strong evidence that the partial / generic identity hardening is
holding under real OSS source.

### 2. Semantic preview expands coupling coverage without changing grade yet

Across all three repositories, `semantic-preview` found substantially more
internal couplings than `syntax`, but the issue counts and final grade did not
change.

Interpretation:

- symbol-aware resolution is connecting more references than syntax-only mode
- current issue heuristics are still dominated by density thresholds and major
  structural hotspots, so the extra couplings are not yet moving the top-line
  result on these samples

### 3. `commandline` shows recoverable workspace diagnostics on Linux/macOS-style environments

`commandlineparser/commandline` emitted 6 recoverable diagnostics in
`semantic-preview`:

- 3 `workspace-failure`
- 3 `workspace-warning`

The failures were caused by missing `.NETFramework` developer packs for
`v4.0`, `v4.5`, and `v4.6.1`, followed by unresolved `mscorlib.dll` metadata
reference warnings.

Observed effect:

- report generation still completed
- headline grade / issue counts remained stable
- file count dropped from `69` to `68` under semantic preview

This validates the recoverable-diagnostic path, but it also means Phase 3c
should treat diagnostics as first-class compare output rather than incidental
noise.

## Implications for Phase 3c

Completed by this run:

- compare `syntax` and `semantic-preview` on 3 Phase 2 OSS targets
- record grade / issue deltas

Still suggested before closing Phase 3:

1. surface semantic diagnostics more prominently in compare artifacts
2. decide whether large coupling-count increases need a dedicated explanation in
   summary / JSON metadata
3. add a perf note while running semantic compare on larger repositories

## Artifact Locations

Temporary compare artifacts were generated under:

```text
/private/tmp/dotnet-coupling-phase3-oss-compare/reports/
  GuardClauses/
  FluentResults/
  commandline/
```
