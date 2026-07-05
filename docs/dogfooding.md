# Dogfooding Guide

Use this guide to collect release evidence before publishing a new version.

## Self Dogfood

The `dogfood` GitHub Actions workflow runs weekly and on demand. It packs the
local tool, installs it from the generated package, and uploads:

- `dogfood-summary.txt`
- `dogfood-no-git-summary.txt`
- `dogfood-report.json`
- `dogfood-report.sarif`
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

For semantic characterization, keep one small synthetic compare target as well.
Real repositories are useful for stability and compatibility checks, but a tiny
synthetic target is better for explaining an intentional semantic delta when the
large targets happen to show no headline result change.

## Phase 4 PR Feedback Dogfood

Before the `0.4.0` release candidate, verify the PR feedback outputs on self
and three public C# repositories:

- `https://github.com/LuckyPennySoftware/MediatR`
- `https://github.com/FluentValidation/FluentValidation`
- `https://github.com/Humanizr/Humanizer`

Use `/private/tmp/dotnet-coupling-phase4-dogfood` for local clones and generated
artifacts. Install the locally packed tool into a temporary tool path, then run:

```bash
dotnet-coupling --summary --no-git ./src
dotnet-coupling --json --hotspots 5 --no-git ./src > dotnet-coupling-report.json
dotnet-coupling --sarif --output dotnet-coupling-report.sarif --no-git ./src
dotnet-coupling --hotspots 5 --no-git ./src > dotnet-coupling-hotspots.txt
```

For suppression smoke testing, add a temporary `.coupling.json` in the dogfood
workspace with one precise `ignore.issues` entry copied from an observed issue,
rerun `--summary --json --sarif`, and verify:

- active issue counts and `--check` exclude the suppressed issue
- JSON includes `suppressedIssues`
- SARIF does not include the suppressed issue

Record the results in `docs/dogfooding/phase4-pr-feedback-dogfooding-2026-07-04.md`.
Keep the commit SHA, analyzed path, grade, issue counts, SARIF generation
result, Hotspots Top 5, and suppression smoke outcome for each target.

## Phase 5 Complexity Hotspots Dogfood

Before the `0.5.0` release candidate, compare pre-change and current
`--hotspots` output on self, non-DDD OSS, and DDD/sample targets.

Required target profile:

- self: `dotnet-coupling` with `.coupling.toml`
- non-DDD OSS: Humanizer and FluentValidation
- DDD/sample: eShopOnWeb and IDDD Samples
- fallback: CleanArchitecture when an SDK or restore issue blocks one target

Use `/private/tmp/dotnet-coupling-phase5-complexity-dogfood` for local clones,
tool installs, and generated artifacts. Reuse the Phase 5 prototype configs
from `/private/tmp/dotnet-coupling-role-prototypes/configs` when available.

Commands:

```bash
dotnet-coupling --json --hotspots 10 --config .coupling.toml --mode syntax .
dotnet-coupling --json --hotspots 10 --config .coupling.toml --mode semantic dotnet-coupling.slnx
dotnet-coupling --json --hotspots 10 --no-git --config /path/to/prototype.toml /path/to/repo
```

Record for each target:

- repository URL and commit SHA
- command, mode, analyzed path, and config path
- grade and issue counts before/after complexity-assisted hotspots
- Hotspots Top 10 before/after, priority score changes, and complexity reasons
- whether rank changes are explainable by high coupling, volatility, and complexity
- workspace diagnostics for semantic mode instead of treating SDK warnings as tool failures

Record the results in
`docs/dogfooding/phase5-complexity-hotspots-dogfooding-2026-07-05.md`.

## External Sample Dogfood

Run against 2-3 small C# repositories before publishing a new alpha or stable
release.

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
dotnet-coupling --sarif --output dotnet-coupling-report.sarif --no-git ./src
dotnet-coupling --hotspots 5 --no-git ./src > dotnet-coupling-hotspots.txt
dotnet-coupling --check --min-grade B ./src
```

Record for each target:

- repository URL and commit SHA
- command used
- grade and issue counts
- SARIF and hotspots artifact paths
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
