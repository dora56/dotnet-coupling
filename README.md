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

## Development

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
dotnet tool restore
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

## MVP Blind Spots

- Semantic symbol resolution is not enabled.
- Runtime DI container dependencies are not analyzed.
- Reflection and `dynamic` calls may be incomplete.
- Generated code is excluded by default.
