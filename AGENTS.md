# Repository Guidelines

## Project Structure & Module Organization

`src/DotnetCoupling.Cli/` contains the single CLI project for MVP. `Program.cs` is the entrypoint using `System.CommandLine`. Core analysis lives in `Analysis/` with `Models.cs` (data types), `CSharpDependencyAnalyzer.cs` (Roslyn syntax walker), `GitVolatility.cs` (git log parsing + temporal co-change), and `ReportRenderer.cs` (text/summary/JSON output). Tests live in `tests/DotnetCoupling.Tests/` with fixture projects under `tests/fixtures/`. Golden test expected outputs are in `tests/golden/`.

## Build, Test, and Development Commands

```bash
# Development
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes

# Run
dotnet run --project src/DotnetCoupling.Cli -- ./src
dotnet run --project src/DotnetCoupling.Cli -- --summary ./src
dotnet run --project src/DotnetCoupling.Cli -- --json ./src
dotnet run --project src/DotnetCoupling.Cli -- --check --min-grade B ./src

# Pack & local install
dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj -c Release
dotnet tool install --global dotnet-coupling --add-source src/DotnetCoupling.Cli/nupkg
```

## Coding Style & Naming Conventions

Target .NET 10 with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Follow standard C# conventions: `PascalCase` for types, methods, properties; `camelCase` for locals and parameters; `_camelCase` for private fields. Use `sealed record` for immutable data models. Prefer file-scoped namespaces. No `var` for non-obvious types in public APIs. Keep methods short; prefer early returns over deep nesting.

## Testing Guidelines

Use xUnit for all tests. Unit tests go in `tests/DotnetCoupling.Tests/`. Fixture C# projects for integration testing go in `tests/fixtures/`. Golden file tests compare CLI output against `tests/golden/*.txt` and `tests/golden/*.json`. JSON golden tests must be property-order independent. After analyzer or scoring changes, run the tool against `tests/fixtures/` and verify output makes sense.

## Commit & Pull Request Guidelines

Follow Conventional Commit style: `feat(analysis): ...`, `fix(scoring): ...`, `test: ...`, `docs: ...`, `ci: ...`, `chore: ...`. Keep commit subjects imperative and scoped. Before pushing, ensure `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` all pass. PRs should summarize user-visible changes and note any JSON schema changes.

## Agent-Specific Notes

Start with `.agents/README.md` as the agent-agnostic hub, then read `.agents/docs/` and `.agents/rules/` for domain guidance and coding rules. Existing `.claude/` and `.github/` files remain for tool-specific compatibility. The design specification lives in `docs/design/` (13 split files by concern). Treat the current source tree as the source of truth.
