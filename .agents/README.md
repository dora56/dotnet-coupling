# Agent Hub

This directory is the primary entrypoint for AI agents working in this repository.

## Start Here

1. Read `../AGENTS.md` for repository workflow and commands.
2. Read `rules/csharp.md` for coding conventions.
3. Read `docs/khononov-framework.md` for scoring intent.
4. Read `docs/issue-types.md` for detection rules and severities.

## Design Specification (split)

The full design spec is split by concern under `../docs/design/`:

| File | When to read |
|------|--------------|
| `00-overview.md` | Purpose, goals, users |
| `01-cli-spec.md` | CLI options, exit codes |
| `02-distribution.md` | NuGet packaging |
| `03-architecture.md` | Pipeline, Roslyn, granularity |
| `04-data-model.md` | C# data types |
| `05-scoring.md` | Strength, Distance, Volatility, Grade |
| `06-issue-detection.md` | Issue types, severity, circular deps |
| `07-output-formats.md` | Text, summary, JSON schema |
| `08-config.md` | Config file, external deps |
| `09-future-features.md` | Baseline, Hotspots, AI, SARIF |
| `10-engineering.md` | Perf, security, testing, CI |
| `11-roadmap.md` | Versioning, phases |
| `12-reference.md` | Mapping table, blind spots, risks |

Start with `05-scoring.md` for the core algorithm, or `03-architecture.md` for implementation structure.

## Scope

- This directory is agent-agnostic and intended for Copilot/Claude/other coding agents.
- Existing `.claude/` and `.github/` files remain for tool-specific compatibility.

## Source of Truth

- Functional specification: `../docs/design/` (split files are primary)
- Index with full content: `../docs/design/dotnet-coupling-design.md`
- If any contradiction exists, the split files win.
