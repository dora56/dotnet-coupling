# dotnet-coupling

Experimental coupling balance analyzer for C#/.NET projects.

`dotnet-coupling` scans C# source with Roslyn syntax analysis and reports coupling risks using three dimensions:

- integration strength
- distance
- volatility from Git history

By default, `dotnet-coupling` runs in `syntax` mode for broad compatibility.
Phase 3 also adds an opt-in `semantic` preview for `.csproj`, `.sln`, and
`.slnx` inputs so project boundaries and symbol-aware dependencies can be
inspected without changing the default contract.

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
dotnet-coupling --mode semantic --summary ./sample.sln
```

## Output

The default report shows the project grade, average balance score, issue counts, and top coupling issues.

JSON output includes:

- `$schema` and `schemaVersion`
- analysis metadata
- issue-density grade
- issue counts and issue details
- manifest run notes and blind spots
- optional project-model metadata for project / assembly / package boundaries

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

## Current Blind Spots

- `syntax` mode does not use semantic symbol resolution.
- `semantic-preview` resolves many symbol-aware dependencies, but some flows are
  still syntax-equivalent or unresolved.
- Runtime DI container dependencies are not analyzed.
- Reflection and `dynamic` calls may be incomplete.
- Generated code is excluded by default.

## License

MIT. See `LICENSE`.
