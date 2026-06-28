# dotnet-coupling 0.3.0

`0.3.0` closes Phase 3 and promotes the project-model and semantic-preview
foundation from roadmap work into the released tool.

## Included

- project-model loading for `.csproj`, `.sln`, and `.slnx` inputs
- explicit `--mode syntax|semantic` with `syntax` as the default
- semantic preview loading through `MSBuildWorkspace`
- project / assembly / package metadata in JSON output
- workspace load diagnostics as recoverable report evidence
- syntax vs semantic compare artifacts for self, OSS, and synthetic dogfooding
- ArchUnitNET boundary tests plus semantic regression coverage for reflection,
  service locator, dynamic dispatch, and project-model edge cases
- compare artifact hardening so metric and diagnostic diffs are visible without
  changing the CLI summary contract

## Compatibility Notes

- `syntax` mode remains the default and preserves the existing CLI / JSON
  contract
- `semantic` mode is still a preview and must be enabled explicitly
- JSON schema stays backward-compatible through optional fields such as
  `projectModel` and manifest diagnostics

## Known Limitations

- semantic preview still depends on target workspace prerequisites being
  loadable in the executing environment
- some reflection, runtime DI container, and dynamic flows remain unresolved or
  syntax-equivalent
- semantic compare deltas may expand coupling counts significantly on richer
  codebases; use the compare artifacts to inspect whether the delta is expected
- SARIF, hotspots, impact, trace, and complexity-assisted prioritization remain
  future features
