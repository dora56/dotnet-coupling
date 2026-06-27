# dotnet-coupling

Experimental coupling balance analyzer for C#/.NET projects.

`dotnet-coupling` scans C# source with Roslyn syntax analysis and reports coupling risks using three dimensions:

- integration strength
- distance
- volatility from Git history

The MVP is intentionally syntax-only. It does not use semantic symbol resolution, MSBuild workspace loading, or DI container runtime analysis yet.

## Install

```bash
dotnet tool install --global dotnet-coupling
```

## Usage

```bash
dotnet-coupling ./src
dotnet-coupling --summary ./src
dotnet-coupling --json ./src
dotnet-coupling --check --min-grade B ./src
dotnet-coupling --no-git ./src
dotnet-coupling --config .coupling.json ./src
dotnet-coupling --check --baseline main --fail-on High ./src
```

## Output

The default report shows the project grade, average balance score, issue counts, and top coupling issues.

JSON output includes:

- `$schema` and `schemaVersion`
- analysis metadata
- issue-density grade
- issue counts and issue details
- syntax-only manifest and blind spots

The schema files live under `schemas/`.

## Configuration

Phase 2 supports JSON configuration via `--config <file>`, `.coupling.json`, or
`coupling.json`. The supported MVP settings are analysis excludes, fan-in/out
thresholds, temporal coupling thresholds, scattered external breadth, and
ignore rules for paths, namespaces, and issue types. See `.coupling.example.json`
and `schemas/dotnet-coupling-config-0.2.schema.json`.

## Baseline Gate

`--baseline <ref>` compares current issues with a Git ref and classifies issues
as new, resolved, or unchanged. With `--check --baseline`, the gate fails only
for new issues at `--fail-on` severity or higher. If `--fail-on` is omitted, the
baseline gate uses `High`.

## Development

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
dotnet test --configuration Release --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/Coverage
dotnet tool restore
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

Mutation score is the primary test-quality signal. Coverage is collected as a
supporting signal to find unexercised areas, not as the main quality gate.
CI enforces the mutation threshold through `stryker-config.json`; coverage is
collected without a threshold.
CI uploads `coverage-report` and `mutation-report` artifacts for inspection.

## MVP Blind Spots

- Semantic symbol resolution is not enabled.
- Runtime DI container dependencies are not analyzed.
- Reflection and `dynamic` calls may be incomplete.
- Generated code is excluded by default.

## License

MIT. See `LICENSE`.
