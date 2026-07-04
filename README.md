# dotnet-coupling

[![NuGet Version](https://img.shields.io/nuget/v/dotnet-coupling?logo=nuget)](https://www.nuget.org/packages/dotnet-coupling)
[![NuGet Downloads](https://img.shields.io/nuget/dt/dotnet-coupling?logo=nuget)](https://www.nuget.org/packages/dotnet-coupling)
[![CI](https://img.shields.io/github/actions/workflow/status/dora56/dotnet-coupling/ci.yml?branch=main&label=ci)](https://github.com/dora56/dotnet-coupling/actions/workflows/ci.yml)
[![GitHub Release](https://img.shields.io/github/v/release/dora56/dotnet-coupling)](https://github.com/dora56/dotnet-coupling/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

Experimental coupling balance analyzer for C#/.NET projects.

`dotnet-coupling` scans C# source and reports coupling risks using three
dimensions from Vlad Khononov's balancing-coupling model:

- integration strength
- distance
- volatility from Git history

By default, `dotnet-coupling` runs in `syntax` mode for broad compatibility.
It also provides an opt-in `semantic` preview for `.csproj`, `.sln`, and
`.slnx` inputs so project boundaries and symbol-aware dependencies can be
inspected without changing the default contract.

> Experimental project
>
> Grades, thresholds, and detected patterns are still being tuned through
> dogfooding and OSS feedback. Treat the report as decision support, not as a
> proof that a codebase is free of coupling risk.

## Table of Contents

- [Quick Start](#quick-start)
- [What It Reports](#what-it-reports)
- [Common Commands](#common-commands)
- [Syntax and Semantic Modes](#syntax-and-semantic-modes)
- [Configuration](#configuration)
- [Baseline Gate](#baseline-gate)
- [SARIF and Hotspots](#sarif-and-hotspots)
- [Output and Schema](#output-and-schema)
- [CI and Quality Gates](#ci-and-quality-gates)
- [Current Blind Spots](#current-blind-spots)
- [Development](#development)
- [License](#license)

## Quick Start

### 1. Install

```bash
dotnet tool install --global dotnet-coupling
```

### 2. Analyze

```bash
dotnet-coupling ./src
dotnet-coupling --summary ./src
dotnet-coupling --json ./src
```

### 3. Gate in CI

```bash
dotnet-coupling --check --min-grade B ./src
dotnet-coupling --check --baseline main --fail-on High ./src
dotnet-coupling --sarif --output dotnet-coupling.sarif ./src
```

Example summary:

```text
Grade: A | Avg Score: 0.79 | Basis: issue-density
Files: 24 | Types: 63 | Couplings: 201 internal / 0 external
Issues: 0 Critical, 0 High, 12 Medium
Git: disabled (--no-git)
```

## What It Reports

The default report highlights the overall grade, average balance score, issue
counts, and the most important coupling problems found in the analyzed scope.

The analyzer currently focuses on:

- structural dependencies observed from source
- project / assembly / NuGet package boundaries in semantic mode
- Git co-change data for volatility and hidden coupling heuristics
- issue patterns such as global complexity, circular dependency, hidden
  coupling, scattered external coupling, and fan-in / fan-out concentration

## Common Commands

```bash
# Default text report
dotnet-coupling ./src

# Summary only
dotnet-coupling --summary ./src

# Machine-readable JSON
dotnet-coupling --json ./src

# GitHub Code Scanning compatible SARIF
dotnet-coupling --sarif --output dotnet-coupling.sarif ./src

# Top refactoring candidates
dotnet-coupling --hotspots 10 ./src

# Skip Git history for faster local runs
dotnet-coupling --no-git ./src

# Apply JSON or TOML config
dotnet-coupling --config .coupling.json ./src
dotnet-coupling --config .coupling.toml ./src

# CI gate on minimum grade
dotnet-coupling --check --min-grade B ./src

# Ratchet gate against a baseline branch
dotnet-coupling --check --baseline main --fail-on High ./src

# Opt in to semantic preview for project-aware analysis
dotnet-coupling --mode semantic --summary ./sample.sln
```

## Syntax and Semantic Modes

`syntax` mode is the default and is the stable compatibility path today.

- works well against plain source directories
- avoids workspace-loading prerequisites
- preserves the established CLI and JSON contract

`semantic` mode is preview-only and must be enabled explicitly.

- accepts `.csproj`, `.sln`, and `.slnx`
- resolves project graph, assembly boundaries, package references, and more
  symbol-aware dependencies
- emits recoverable workspace diagnostics when the environment cannot fully load
  the target

## Configuration

JSON and TOML configuration are supported via `--config <file>`. Automatic
discovery checks `.coupling.json` and `coupling.json` first, then
`.coupling.toml` and `coupling.toml`.

Current supported settings include:

- analysis excludes
- fan-in and fan-out thresholds
- temporal coupling thresholds
- scattered external breadth thresholds
- ignore rules for paths, namespaces, issue types, and precise issue
  suppressions

TOML keys use `snake_case`; JSON keeps the existing `camelCase` schema shape.

See [`.coupling.example.json`](.coupling.example.json),
[`.coupling.example.toml`](.coupling.example.toml),
and [`schemas/dotnet-coupling-config-0.2.schema.json`](schemas/dotnet-coupling-config-0.2.schema.json).

## Baseline Gate

`--baseline <ref>` compares current issues with a Git ref and classifies them
as new, resolved, or unchanged.

With `--check --baseline`, the gate fails only for new issues at `--fail-on`
severity or higher. If `--fail-on` is omitted, the baseline gate uses `High`.

This makes it practical to adopt in an existing codebase without forcing a
one-shot cleanup of all historical debt.

For long-lived accepted debt, use `.coupling.json` `ignore.issues` with a
stable `(type, source, target)` key and a required `reason`. Suppressed issues
are excluded from active counts, grade, `--check`, and SARIF upload, but remain
visible in summary and JSON output.

## SARIF and Hotspots

`--sarif` emits SARIF 2.1.0 using Microsoft's `Sarif.Sdk`, with issue types
mapped to SARIF rules and source locations normalized to repository-relative
URIs for GitHub Code Scanning.

```bash
dotnet-coupling --sarif --output dotnet-coupling.sarif --no-git ./src
```

`--hotspots [N]` ranks the top coupling repair candidates from active issues,
fan-in, fan-out, volatility, boundary crossing, and cycle participation. The
default count is `10`.

```bash
dotnet-coupling --hotspots ./src
dotnet-coupling --json --hotspots 5 ./src
```

## Output and Schema

JSON output includes:

- `$schema` and `schemaVersion`
- analysis metadata
- issue-density grade
- issue counts and issue details
- manifest run notes and blind spots
- optional project-model metadata for project / assembly / package boundaries
- optional hotspots and suppressed issues when requested

Schema files live under [`schemas/`](schemas/).

## CI and Quality Gates

Mutation score is the primary test-quality signal. Coverage is collected as a
supporting signal to find unexercised areas, not as the main quality gate.

Current CI posture:

- `pull_request`: build, test, format, package smoke, report aggregation, and coupling feedback
- `main` push: build, test, format, package smoke, and report aggregation
- same-repo PR / main push: coupling SARIF upload to GitHub Code Scanning
- `nightly-mutation`: full Stryker run on schedule or manual dispatch
- `release`: build, test, format, pack, local tool smoke, publish

CI uploads `coverage-report`, `dotnet-coupling-sarif`, `dotnet-coupling-hotspots`,
and dogfood artifacts for inspection. The PR summary includes coverage and
coupling feedback; mutation appears as not available in PR CI and is reported by
the nightly workflow instead.

## Current Blind Spots

- `syntax` mode does not use semantic symbol resolution
- semantic preview still depends on workspace prerequisites being loadable
- runtime DI container dependencies are not analyzed
- reflection and `dynamic` calls may be incomplete
- generated code is excluded by default

Treat a clean report as "no observed issues", not as a guarantee that no
coupling risk exists.

## Development

```bash
dotnet restore dotnet-coupling.slnx --locked-mode
dotnet build dotnet-coupling.slnx --configuration Release --no-restore
dotnet test dotnet-coupling.slnx --configuration Release --no-restore
dotnet format dotnet-coupling.slnx --verify-no-changes --no-restore
dotnet test dotnet-coupling.slnx --configuration Release --results-directory TestResults/Coverage --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura
dotnet tool restore
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

Release history is collected in [`CHANGELOG.md`](CHANGELOG.md).

## License

MIT. See [`LICENSE`](LICENSE).
