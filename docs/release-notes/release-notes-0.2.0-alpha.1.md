# dotnet-coupling 0.2.0-alpha.1

`0.2.0-alpha.1` is the Phase 2 public alpha for feedback-driven team use.

## Included

- JSON configuration via `--config`, `.coupling.json`, and `coupling.json`.
- Configurable fan-in/out, temporal coupling, and scattered external coupling thresholds.
- Ignore rules for paths, namespaces, and issue types.
- Baseline comparison via `--baseline <ref>`.
- Ratchet gate via `--check --baseline`, failing only on new issues at the configured severity threshold.
- JSON report schema version `0.2` when baseline data is present.
- JSON config schema `dotnet-coupling-config-0.2.schema.json`.

## Known Limitations

- Semantic symbol resolution is not enabled.
- Baseline comparison uses syntax-only issue keys and excludes Hidden Coupling from diffing.
- TOML configuration is not supported yet.
- SARIF, hotspots, impact, trace, and complexity-assisted prioritization remain future features.
