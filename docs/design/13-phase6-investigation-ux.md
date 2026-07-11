# Phase 6 Investigation UX

## Objective

Phase 6 turns the analysis report into an investigation aid that answers two
questions without changing the project health gate:

1. What depends on the component I am about to change?
2. Where does a dependency on this type or member come from?

The release target is `0.6.0`.
Complexity remains a prioritization aid and does not affect Grade, issue
severity, `--fail-on`, or `--check`.

## Research Findings

- [dependency-cruiser](https://github.com/sverweij/dependency-cruiser/blob/main/doc/cli.md)
  separates focused graph traversal from report formatting.
  Its `--focus`, `--focus-depth`, and `--reaches` behavior supports a bounded,
  cycle-safe reverse dependency traversal for impact analysis.
- [cargo-coupling](https://github.com/nwiizo/cargo-coupling/blob/main/README.md)
  treats hotspots, impact, trace, AI output, Japanese output, and Markdown as
  job-focused views over one analysis result.
- The current `dotnet-coupling` report already contains components, resolved
  couplings, observations, project metadata, volatility, issues, and optional
  complexity.
  Phase 6 should derive investigation views from this evidence instead of
  running a second analyzer.

## Scope

`0.6.0` includes:

- `--impact <component>` for direct and transitive reverse dependencies.
- `--trace <symbol>` for semantic type/member dependency evidence.
- `--depth <N>` for impact/trace traversal, default `3`; `0` means unlimited.
- `--markdown` for a complete human-readable report.
- `--ai` for deterministic coding-agent handoff output.
- `--jp` / `--japanese` for localized human-readable output.
- configurable complexity thresholds and weight under `prioritization`.
- semantic symbol identity shared by component, observation, and complexity
  collection.
- JSON schema `0.5` with optional `impact` and `trace` fields.

Non-goals:

- No LLM or external service call is made by `--ai`.
- No source-code rewrite or automatic refactoring is performed.
- Grade and issue detection formulas do not change.
- Web UI and default `auto` mode remain future work.
- Method-level control-flow or data-flow analysis is not introduced.

## CLI Contract

```text
dotnet-coupling [path] [options]

--impact <component>   Show direct and transitive dependents.
--trace <symbol>       Trace semantic dependencies on a type or member.
--depth <N>            Maximum traversal depth. Default: 3; 0: unlimited.
--markdown             Render a Markdown report.
--ai                   Render deterministic coding-agent guidance.
--jp, --japanese       Localize human-readable output.
```

Rules:

- `--impact`, `--trace`, and `--hotspots` are mutually exclusive views.
- `--trace` requires `--mode semantic` and project/solution input.
- `--depth` is valid only with `--impact` or `--trace` and must be non-negative.
- `--json` may be combined with `--impact` or `--trace`.
- `--markdown` may be combined with `--impact` or `--trace`.
- `--ai` may be combined with `--impact` or `--trace` and uses the same
  priority evidence.
- `--sarif` cannot be combined with investigation views, Markdown, or AI output.
- `--jp` does not translate JSON or SARIF contracts.
- `--check` keeps its existing exit-code behavior regardless of the selected
  human-readable view.

Component and symbol resolution is deterministic:

1. exact fully-qualified identity
2. exact simple type or member name
3. a unique case-insensitive match

No match returns exit code `2` with suggestions.
Multiple matches return exit code `2` with sorted candidates.

## Application Structure

Core owns graph interpretation and report contracts:

```text
DotnetCoupling.Core
  Contracts/
    ImpactAnalysis.cs
    TraceAnalysis.cs
    DependencyPath.cs
    PrioritizationOptions.cs
    ReportLanguage.cs
  Domain/
    InvestigationTargetResolver.cs
    ImpactAnalyzer.cs
    TraceAnalyzer.cs
    ComplexityPrioritizer.cs
  Reporting/
    MarkdownReportRenderer.cs
    AiReportRenderer.cs
    InvestigationReportRenderer.cs
    ReportText.cs
```

Roslyn owns semantic identity collection:

```text
DotnetCoupling.Roslyn/Collection
  SymbolIdentity.cs
  CSharpSyntaxDependencyCollector.cs
  CSharpComplexityCollector.cs
```

The CLI remains the composition root.
Core does not depend on Roslyn, Git, SARIF, or System.CommandLine.

## Graph Semantics

Impact traverses resolved internal couplings in reverse:

```text
changed target <- direct dependent <- transitive dependent
```

- Breadth-first traversal records the shortest path and depth.
- Components are visited once, so cycles terminate deterministically.
- Duplicate couplings do not duplicate affected components.
- Results are ordered by depth, then fully-qualified component identity.
- Impacted project and namespace sets are distinct and sorted.

Trace uses semantic observations.
Type trace starts from a component identity.
Member trace starts from observation `TargetSymbol` identities and reports the
source member when available.
After the first semantic observation, reverse component traversal supplies the
transitive caller path.

## Priority Model

The existing hotspot model is preserved, but complexity tuning moves to:

```toml
[prioritization]
cyclomatic_complexity_threshold = 10
cognitive_complexity_threshold = 15
complexity_weight = 0.10
```

JSON uses camelCase equivalents.

- thresholds must be positive integers
- `complexityWeight` must be between `0.0` and `0.5`
- omitted values preserve the `0.5.0` behavior

Impact risk combines blast radius, strongest observed coupling risk, boundary
crossing, volatility, and configured complexity contribution.
The formula and each reason are emitted so ranking changes are explainable.

## Output Contracts

Schema `0.5` extends the report with optional investigation data:

```json
{
  "schemaVersion": "0.5",
  "impact": {
    "query": "Order",
    "component": "Sales.Domain.Order",
    "riskScore": 0.72,
    "reasons": ["8 affected components", "crosses project boundary"],
    "dependents": []
  }
}
```

Existing default JSON remains schema `0.1`.
Baseline remains `0.2`, suppression/hotspots remain `0.3`, and
complexity-bearing hotspots remain `0.4` unless impact or trace is requested.

Markdown is intended for artifacts and pull-request summaries.
AI output is Markdown with stable sections for evidence, why it matters, and
bounded suggested work.
It reuses detected problem/recommendation text and does not invent source code.

## TDD Delivery Plan

1. Prioritization config
   - Red: JSON/TOML config tests for thresholds, weight, unknown keys, and
     invalid ranges.
   - Green: add `PrioritizationOptions` and shared validation.
2. Semantic identity
   - Red: partial, nested, generic, overloaded-member, and alias tests.
   - Green: share symbol identity between component, observation, and
     complexity collectors.
3. Impact analysis
   - Red: direct, transitive, cycle, duplicate edge, depth, ambiguity, and
     complexity-risk tests.
   - Green: implement reverse BFS and explainable risk.
4. Trace analysis
   - Red: type/member trace, overload aggregation, transitive callers,
     ambiguity, and syntax-mode rejection.
   - Green: add semantic observation identities and trace traversal.
5. Renderers and schemas
   - Red: text, Markdown, AI, Japanese, JSON `0.5`, and backward-compatibility
     golden/schema tests.
   - Green: add focused renderers while retaining `ReportRenderer` entrypoints.
6. CLI behavior
   - Red: option combinations, precedence, exit codes, output files, missing and
     ambiguous queries, depth boundaries, and `--check` invariance.
   - Green: wire options in the CLI composition root.

## Quality and Release Gate

- required roles: product manager, senior engineer, QA engineer
- activated roles: application architect, technical writer
- all five roles accept this scope because the user value, dependency
  direction, executable proof, compatibility guardrails, and documentation
  surfaces are explicit
- adversarial review uses code-quality, architecture, QA, performance, and
  CLI-contract lenses
- dogfooding targets self plus Humanizer, FluentValidation, MediatR, and one DDD
  sample where semantic workspace loading is viable
- mutation remains nightly/local and is run locally for changed Core graph and
  prioritization logic

Release requires:

- locked restore, Release build, all tests, format, pack, and local tool smoke
- syntax/semantic self comparison
- multiple-OSS impact/trace/Markdown/AI evidence
- no Grade or issue-count change caused only by investigation output
- PR and main CI green
- signed release commit and signed `v0.6.0` tag
- Trusted Publishing success and NuGet.org install smoke
