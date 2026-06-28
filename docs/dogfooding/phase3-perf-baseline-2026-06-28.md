# Phase 3 Preliminary Perf Baseline - 2026-06-28

## Summary

Overall: PARTIAL_BASELINE_RECORDED

Phase 3c の `大規模 repo の perf baseline` に向けて、large SDK-style target として
`dotnet/efcore` の `src/EFCore/EFCore.csproj` を sampled run した。

Observed result:

- `syntax` mode は 1105 C# files を **1.37s** で完走
- `semantic-preview` は **0.27s** で失敗
- failure reason は `MSBuildWorkspace` の解析対象 repo が要求する SDK を
  hostfxr が解決できなかったこと

この結果から、Phase 3c の perf baseline は「speed」だけでなく
**semantic workspace load preconditions** を一緒に記録すべきことが分かった。

## Environment

- Tool: locally packed `dotnet-coupling 0.2.0-alpha.1`
- Date: 2026-06-28
- Compare workspace: `/private/tmp/dotnet-coupling-phase3-oss-compare/efcore`
- Repository: `dotnet/efcore`
- Commit: `0bb1c8df9d3e0202bbc77f5edd9fc5b617d51d17`
- Target: `src/EFCore/EFCore.csproj`
- File count: `1105` C# files

## Commands

```bash
/usr/bin/time -p dotnet-coupling --mode syntax --summary --no-git /private/tmp/dotnet-coupling-phase3-oss-compare/efcore/src/EFCore/EFCore.csproj
/usr/bin/time -p dotnet-coupling --mode semantic --summary --no-git /private/tmp/dotnet-coupling-phase3-oss-compare/efcore/src/EFCore/EFCore.csproj
```

## Measurements

| Mode | Result | Wall time |
| --- | --- | --- |
| `syntax` | PASS | `1.37s` |
| `semantic-preview` | FAIL | `0.27s` |

## Semantic Failure Detail

`semantic-preview` failed while opening the project through `MSBuildWorkspace`:

- exception type: `System.InvalidOperationException`
- hostfxr detail: `hostfxr_resolve_sdk2`
- top-level message: `The required .NET SDK wasn't found. Please run ./restore.sh or .\\restore.cmd to install it.`

Interpretation:

- syntax-only analysis continues to be cheap and robust on large modern codebases
- semantic preview currently depends on the target repo's local SDK / restore
  prerequisites being satisfiable in the executing environment

## Implications

Before Phase 3 can claim a stable large-repo perf baseline, we likely need one
of these:

1. a semantic benchmark target whose SDK requirements are satisfied out of the box
2. clearer semantic diagnostics / fallback behavior when the target workspace
   cannot be loaded
3. a documented distinction between `load-blocked` and `measured` semantic runs

This run is useful evidence, but it is **not yet sufficient** to mark the
Phase 3c perf baseline item complete.

## Follow-up

Perf baseline collection is now scriptable through
`scripts/generate-perf-baseline-report.sh`, so future large-repo runs can
capture timing, stdout/stderr, and load-blocked semantic failures in a uniform
artifact shape.

## Scripted Rerun

The scripted baseline was rerun against the same target after the reporting
script landed:

```bash
scripts/generate-perf-baseline-report.sh \
  src/DotnetCoupling.Cli/bin/Release/net10.0/publish/DotnetCoupling.Cli \
  /private/tmp/dotnet-coupling-phase3-oss-compare/efcore/src/EFCore/EFCore.csproj \
  /private/tmp/dotnet-coupling-phase3-oss-compare/efcore-perf-baseline
```

Observed result:

- `syntax` mode: PASS in `1.58s`
- `semantic-preview`: FAIL(exit=`4`) in `0.26s`
- failure class remains `hostfxr_resolve_sdk2` / missing required SDK

This confirms that the remaining Phase 3c gap is no longer artifact collection.
It is specifically **semantic workspace loadability on representative large
repositories**.

After `fix(cli): stabilize semantic workspace load failures`, the same large
target now fails with a **stable single-line semantic workspace error** instead
of an `Unexpected analysis error` banner followed by a stack trace. The
loadability problem remains, but the failure mode is now fit for CI logs and
perf artifacts.

The perf baseline script now also classifies this state explicitly as
`LOAD_BLOCKED(exit=4)`, which makes the difference between `measured semantic
run` and `semantic workspace precondition failure` visible in a single table.
