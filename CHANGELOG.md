# Changelog

All notable changes to `dotnet-coupling` are collected here.

## 0.6.1

Patch release focused on analysis and CI feedback reliability.

### Changed

- Cyclomatic and Cognitive Complexity now follow the `sonar-dotnet 10.27`
  C# metric profile for supported member scopes, including logical sequences,
  nesting, recursion, local functions, null-flow operators, and C# patterns.
- Namespace cycle detection now uses analyzed component metadata instead of
  inferring namespaces by trimming component identifiers, preventing nested
  types from creating pseudo-namespace cycles.
- Coverage reporting uses octocov for line coverage, main-branch diffs, Job
  Summary, and same-repository PR comments. Branch coverage remains visible as
  an octocov custom metric.
- Self-benchmark CI now stores a semantic report, enforces a Grade C floor, and
  rejects new High or Critical issues relative to `origin/main`.
- Nightly mutation testing now runs separate Core and Roslyn complexity suites.

### Compatibility Notes

- Complexity values and hotspot order can change because the metric profile was
  corrected. Complexity still does not affect Grade, issue severity, issue
  count, `--fail-on`, or `--check`.
- CLI options, exit codes, JSON schema shapes, and NuGet package identity are
  unchanged.
- Syntax mode remains the default and semantic mode remains explicit preview
  functionality.

## 0.6.0

Phase 6 turns coupling reports into an investigation aid while preserving the
existing project health gate.

### Added

- `--impact <component>` with bounded, cycle-safe reverse dependency paths,
  affected project/namespace sets, and explainable risk reasons.
- `--trace <symbol>` for semantic type/member caller evidence, including direct
  source member and location data.
- `--depth <N>` for impact/trace traversal; the default is `3` and `0` is
  unlimited.
- Markdown reports, deterministic coding-agent handoff through `--ai`, and
  Japanese human-readable output through `--jp` / `--japanese`.
- Configurable cyclomatic/cognitive thresholds and complexity weight under
  `prioritization` for Hotspots, Impact, and AI priority evidence.
- JSON report schema `0.5` with optional `impact` and `trace` fields.
- Self, Humanizer, FluentValidation, MediatR, and eShopOnWeb dogfooding evidence.

### Changed

- Semantic observations now retain stable source and target member identities,
  including methods, constructors, accessors, local functions, and operators.
- AI and Markdown output preserve observed issue evidence, recommendations,
  locations, priority reasons, diagnostics, and analysis blind spots.
- Circular-dependency hotspots are ranked by actual namespace participant
  instead of treating a rendered `A -> B` cycle path as a component.
- Impact boundary state is preserved across transitive paths and traversed-edge
  scoring is linear in the analyzed graph size.

### Compatibility Notes

- `syntax` remains the default mode; `--trace` requires explicit semantic mode.
- Grade, issue severity, issue counts, `--fail-on`, and `--check` are unchanged
  by investigation and presentation options.
- Existing report schemas `0.1` through `0.4` remain selected for their existing
  scenarios. Schema `0.5` is used only when impact or trace data is requested.
- The previous public `AnalysisOptions` constructor remains available and uses
  default prioritization values.
- NuGet package identity and existing CLI options remain unchanged.

## 0.5.0

Phase 5 adds config-informed prioritization so coupling findings can reflect
the system's domain context and the complexity of the code that must be changed.

### Added

- TOML configuration through explicit `--config` files and automatic discovery
  of `.coupling.toml` / `coupling.toml`, implemented with Tomlyn.
- Domain Context configuration for subdomain category, expected volatility,
  strategic boundary hints, and technical area roles.
- `AccidentalVolatility` detection to distinguish expected business change from
  churn caused by design or implementation friction.
- Role-aware score calibration for contracts and composition roots while
  preserving observed coupling evidence.
- Domain Context and Role Context coverage in summary and JSON output so partial
  configuration remains visible.
- Syntax-based cyclomatic and cognitive complexity metrics for methods and types.
- Complexity-assisted `--hotspots` ranking with explicit priority reasons.
- JSON report schema `0.4` with optional `hotspots[].complexity` data.
- Self, OSS, and DDD sample dogfooding evidence for Domain Context calibration
  and complexity-assisted ranking.

### Changed

- Configuration loading now normalizes JSON and TOML into the same validated
  internal model, including unknown-property and invalid-value diagnostics.
- Hotspot priority can be increased by high complexity, but complexity does not
  change Grade, issue severity, issue count, `--fail-on`, or `--check` behavior.
- Core, Git, and Roslyn sources are organized by responsibility, with compiled
  architecture and structural fitness tests protecting module boundaries.

### Compatibility Notes

- `syntax` mode remains the default; `semantic` mode remains explicit preview
  functionality.
- Existing JSON configuration and report schemas `0.1` through `0.3` remain
  supported. Schema `0.4` is selected only for complexity-bearing hotspot JSON.
- Existing CLI option names, exit codes, and NuGet package identity are unchanged.

## 0.4.0

Phase 4 moves `dotnet-coupling` into PR feedback and team workflow use.

### Added

- SARIF 2.1.0 output through `--sarif`, generated with Microsoft's `Sarif.Sdk`.
- GitHub Code Scanning friendly SARIF paths, rules, severity levels, and stable partial fingerprints.
- `--hotspots [N]` for ranking active coupling repair candidates.
- JSON report schema `0.3` with optional `hotspots` and `suppressedIssues`.
- Precise `.coupling.json` suppressions via `ignore.issues` with required reasons.
- GitHub Actions SARIF and baseline gate examples.
- CI coupling feedback artifacts for SARIF and hotspots.

### Changed

- Suppressed issues are excluded from active issue counts, grade calculation, `--check`, and SARIF upload while remaining visible in summary and JSON output.
- CI report aggregation now includes coverage and coupling feedback, plus mutation availability status when no Stryker report is produced.
- Test execution is aligned with Microsoft Testing Platform.
- Mutation moved out of PR CI into a scheduled/manual `nightly-mutation` workflow; local Stryker runs remain supported for focused checks.

### Compatibility Notes

- `syntax` mode remains the default.
- `semantic` mode remains preview-only and must be enabled explicitly.
- Existing CLI options, NuGet package identity, and JSON schema `0.1` / `0.2` contracts remain compatible.

## 0.3.1

Patch release for release-workflow hardening and public entrypoint documentation.

### Added

- Expanded README with badges, quick start, CI usage, baseline guidance, and syntax vs semantic mode explanation.
- `self-benchmark` CI job that verifies the packed tool can analyze the repository with `--check --min-grade A --no-git`.

### Changed

- GitHub Actions artifact upload updated to `actions/upload-artifact@v7`.
- CI local tool install steps read the package version from `Directory.Build.props` instead of hardcoding `0.3.0`.

### Compatibility Notes

- CLI options and behavior are unchanged from `0.3.0`.
- JSON schema version remains unchanged.
- `semantic` mode remains preview-only.

## 0.3.0

Phase 3 release for project-model loading and semantic-preview foundation.

### Added

- Project-model loading for `.csproj`, `.sln`, and `.slnx` inputs.
- Explicit `--mode syntax|semantic` with `syntax` as the default.
- Semantic preview loading through `MSBuildWorkspace`.
- Project / assembly / package metadata in JSON output.
- Workspace load diagnostics as recoverable report evidence.
- Syntax vs semantic compare artifacts for self, OSS, and synthetic dogfooding.
- ArchUnitNET boundary tests and semantic regression coverage for reflection, service locator, dynamic dispatch, and project-model edge cases.

### Compatibility Notes

- `syntax` mode preserves the existing CLI and JSON contract.
- `semantic` mode is preview-only and must be enabled explicitly.
- JSON schema stays backward-compatible through optional fields such as `projectModel` and manifest diagnostics.

## 0.2.0-alpha.1

Phase 2 public alpha for feedback-driven team use.

### Added

- JSON configuration via `--config`, `.coupling.json`, and `coupling.json`.
- Configurable fan-in/out, temporal coupling, and scattered external coupling thresholds.
- Ignore rules for paths, namespaces, and issue types.
- Baseline comparison via `--baseline <ref>`.
- Ratchet gate via `--check --baseline`, failing only on new issues at the configured severity threshold.
- JSON report schema `0.2` when baseline data is present.
- JSON config schema `dotnet-coupling-config-0.2.schema.json`.

### Known Limitations

- TOML configuration is not supported.
- SARIF, hotspots, impact, trace, and complexity-assisted prioritization were deferred from this release.

## 0.1.0-alpha.1

First public alpha of `dotnet-coupling`.

### Added

- Syntax-only C# coupling analysis.
- Git volatility and temporal co-change analysis.
- Text, summary, and JSON output.
- JSON report schema `0.1`.
- CI check mode with `--check`, `--min-grade`, and `--fail-on`.

### Known Limitations

- Semantic symbol resolution was not enabled.
- `.slnx`, `.sln`, and `.csproj` workspace loading was not enabled.
- Runtime DI container resolution was not analyzed.
- Reflection and `dynamic` calls may be incomplete.
- Generated code is excluded by default.
