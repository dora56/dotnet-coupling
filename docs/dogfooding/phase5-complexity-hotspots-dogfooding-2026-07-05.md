# Phase 5 Complexity Hotspots Dogfooding - 2026-07-05

This dogfood pass validates `0.5.0` complexity-assisted `--hotspots`. The goal
is to improve remediation priority without changing Grade, issue severity, or
`--check` behavior.

## Environment

- Current tool commit: `4e6664b`
- Baseline tool: `origin/main` archived and packed locally with `HUSKY=0`
- Working directory: `/private/tmp/dotnet-coupling-phase6-complexity-dogfood`
- Artifacts: `/private/tmp/dotnet-coupling-phase6-complexity-dogfood/reports`
- Current package: `src/DotnetCoupling.Cli/nupkg/dotnet-coupling.0.4.0.nupkg`

## Results

| Target | Commit | Grade | Issues | Schema | Complexity in Top 10 | Max complexity | Diagnostics |
| --- | --- | --- | --- | --- | ---: | --- | ---: |
| self syntax | `4e6664b` | S -> S | C0 / H0 / M5 / L0 -> C0 / H0 / M5 / L0 | 0.3 -> 0.4 | 4 | CYC 26 / COG 30 | 0 |
| self semantic | `4e6664b` | C -> C | C0 / H12 / M8 / L0 -> C0 / H12 / M8 / L0 | 0.3 -> 0.4 | 10 | CYC 14 / COG 19 | 6 |
| Humanizer syntax | `f9292aa` | S -> S | C0 / H0 / M1 / L0 -> C0 / H0 / M1 / L0 | 0.3 -> 0.3 | 0 | CYC 0 / COG 0 | 0 |
| FluentValidation syntax | `9439790` | C -> C | C0 / H9 / M121 / L0 -> C0 / H9 / M121 / L0 | 0.3 -> 0.4 | 7 | CYC 63 / COG 11 | 0 |
| eShopOnWeb syntax | `4da8212` | D -> D | C0 / H96 / M4 / L0 -> C0 / H96 / M4 / L0 | 0.3 -> 0.4 | 10 | CYC 7 / COG 8 | 0 |
| eShopOnWeb semantic | `4da8212` | D -> D | C0 / H129 / M10 / L0 -> C0 / H129 / M10 / L0 | 0.3 -> 0.4 | 8 | CYC 4 / COG 4 | 1 |
| IDDD Samples syntax | `914a746` | C -> C | C0 / H52 / M47 / L0 -> C0 / H52 / M47 / L0 | 0.3 -> 0.4 | 10 | CYC 7 / COG 9 | 0 |
| IDDD Samples semantic | `914a746` | C -> C | C0 / H61 / M129 / L0 -> C0 / H61 / M129 / L0 | 0.3 -> 0.4 | 10 | CYC 7 / COG 9 | 15 |

## Rank Changes

- Self syntax: `CSharpDependencyAnalyzer.cs` moved from rank 4 to 3 over
  `IssueDetector.cs` because it now carries high cyclomatic/cognitive reasons
  (`CYC 14 / COG 19`). Grade and issue counts stayed unchanged.
- Self semantic: no rank changes in Top 5. The existing top hotspot,
  `DotnetCoupling.Roslyn.CSharpDependencyAnalyzer`, now exposes complexity
  reasons (`CYC 14 / COG 19`) without changing the finding set.
- FluentValidation syntax: `AbstractValidator<T>` moved from rank 2 to 1 over
  `InlineValidator<T>` due to `CYC 10`; `LanguageManager` moved from rank 5 to
  4 due to `CYC 63`. Both changes are explainable and keep Grade/issue counts
  stable.
- Humanizer syntax: the only hotspot is `Humanizer.GrammaticalGender`, which has
  no executable members. Schema remains `0.3`, which is expected because no
  hotspot complexity is emitted.
- eShopOnWeb and IDDD Samples: no Top 5 rank changes. Complexity context is
  added to the existing hotspots and does not disturb the domain-context signal.

## Interpretation

- The main contract held: Grade, issue counts, and exit behavior did not change
  across self, non-DDD OSS, and DDD/sample targets.
- Complexity acts as an explanatory priority assist. It changed ranking only
  where a hotspot already had active coupling issues and a clearly more complex
  implementation body.
- The fixed thresholds are conservative enough for DDD samples. Many domain
  entities now show low complexity metadata, but they are not boosted unless
  they cross the thresholds.
- File-path hotspots from temporal/hidden coupling now receive file-level
  aggregated complexity when the file contains analyzed components. This made
  self syntax dogfood useful, where the top baseline hotspots were file paths.
- Semantic mode diagnostics remain workspace-load evidence, not feature
  failures: eShopOnWeb reports 1 diagnostic and IDDD Samples reports 15, matching
  earlier Phase 5 expectations.

## Commands

Self:

```bash
dotnet-coupling --json --hotspots 10 --config .coupling.toml --mode syntax .
dotnet-coupling --json --hotspots 10 --config .coupling.toml --mode semantic dotnet-coupling.slnx
```

External syntax:

```bash
dotnet-coupling --json --hotspots 10 --no-git --config /private/tmp/dotnet-coupling-role-prototypes/configs/humanizer.coupling.role-prototype.toml /private/tmp/dotnet-coupling-role-prototypes/repos/Humanizer
dotnet-coupling --json --hotspots 10 --no-git /private/tmp/dotnet-coupling-phase4-dogfood/repos/FluentValidation/src
dotnet-coupling --json --hotspots 10 --no-git --config /private/tmp/dotnet-coupling-role-prototypes/configs/eshoponweb.coupling.role-prototype.toml /private/tmp/dotnet-coupling-role-prototypes/repos/eShopOnWeb
dotnet-coupling --json --hotspots 10 --no-git --config /private/tmp/dotnet-coupling-role-prototypes/configs/iddd-samples-net.coupling.role-prototype.toml /private/tmp/dotnet-coupling-role-prototypes/repos/IDDD_Samples_NET
```

DDD semantic:

```bash
dotnet-coupling --json --hotspots 10 --no-git --config /private/tmp/dotnet-coupling-role-prototypes/configs/eshoponweb.coupling.role-prototype.toml --mode semantic /private/tmp/dotnet-coupling-role-prototypes/repos/eShopOnWeb/eShopOnWeb.sln
dotnet-coupling --json --hotspots 10 --no-git --config /private/tmp/dotnet-coupling-role-prototypes/configs/iddd-samples-net.coupling.role-prototype.toml --mode semantic /private/tmp/dotnet-coupling-role-prototypes/repos/IDDD_Samples_NET/IDDD_Samples.sln
```
