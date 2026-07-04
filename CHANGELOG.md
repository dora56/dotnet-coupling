# Changelog

All notable changes to `dotnet-coupling` are collected here.

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
- CI report aggregation now includes coverage, mutation, and coupling feedback sections.
- Test execution is aligned with Microsoft Testing Platform.
- Mutation remains PR-focused and uses Stryker `since` for diff-scoped feedback.

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
