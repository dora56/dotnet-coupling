# Dogfooding Guide

Use this guide to collect Phase 2 public alpha feedback.

## Self Dogfood

The `dogfood` GitHub Actions workflow runs weekly and on demand. It packs the
local tool, installs it from the generated package, and uploads:

- `dogfood-summary.txt`
- `dogfood-no-git-summary.txt`
- `dogfood-report.json`
- `dogfood-compare/semantic-compare.md`
- `dogfood-compare/syntax-summary.txt`
- `dogfood-compare/semantic-summary.txt`
- `dogfood-compare/syntax-report.json`
- `dogfood-compare/semantic-report.json`

The compare artifact runs against `dotnet-coupling.slnx` so that `syntax` and
`semantic-preview` can be inspected side by side on the same self-dogfood target.
The generated markdown should capture both headline differences and a compact
metric/diagnostic diff so that semantic-only workspace warnings are visible as
compare evidence rather than hidden in stderr.

For Phase 3 semantic characterization, keep one small synthetic compare target
as well. Real repositories are useful for stability and compatibility checks,
but a tiny synthetic target is better for explaining an intentional semantic
delta when the large targets happen to show no headline result change.

## External Sample Dogfood

Run against 2-3 small C# repositories before publishing a new alpha.

Recommended target profile:

- small enough to inspect findings manually
- has normal Git history
- has at least two namespaces or projects
- does not require private package feeds to inspect source

Commands:

```bash
dotnet-coupling --summary ./src
dotnet-coupling --summary --no-git ./src
dotnet-coupling --json --no-git ./src > dotnet-coupling-report.json
dotnet-coupling --check --min-grade B ./src
```

Record for each target:

- repository URL and commit SHA
- command used
- grade and issue counts
- false positives with issue type and source/target
- false negatives with expected issue type
- confusing output or missing context

Use the GitHub issue templates for false positives and false negatives.

## Perf Baseline

Use the perf baseline script when comparing `syntax` and `semantic-preview` on
larger SDK-style targets.

Command:

```bash
scripts/generate-perf-baseline-report.sh \
  /path/to/dotnet-coupling \
  /path/to/target.csproj \
  /path/to/output-directory
```

Artifacts:

- `perf-baseline.md`
- `syntax-summary.txt`
- `semantic-summary.txt`
- `syntax.stderr.txt`
- `semantic.stderr.txt`
- `syntax.time.txt`
- `semantic.time.txt`

The script records both timing and failure evidence. A semantic run that cannot
load its workspace is still useful data; keep the generated failure excerpt and
environment notes instead of discarding the run.

When the semantic run fails specifically because the workspace could not be
loaded, the report classifies it as `LOAD_BLOCKED(exit=4)` instead of a generic
failure so it is easier to separate environment/setup problems from measured
semantic performance.
