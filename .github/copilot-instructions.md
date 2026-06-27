# GitHub Copilot Instructions for dotnet-coupling

## Project Overview

`dotnet-coupling` is a C#/.NET CLI tool that analyzes coupling quality in .NET codebases using the Khononov Balance Framework (Strength × Distance × Volatility). It is a .NET port of [cargo-coupling](https://github.com/nwiizo/cargo-coupling).

## Architecture

- **Target**: .NET 10 (`net10.0`)
- **Approach**: Roslyn syntax-only parsing (no `SemanticModel` / MSBuildWorkspace in MVP)
- **CLI**: `System.CommandLine`
- **Output**: Structured JSON report + human-friendly summary

## Key Conventions

- Use `sealed record` for data models (coupling entries, issue reports, config)
- Use `sealed class` unless inheritance is explicitly needed
- File-scoped namespaces
- Nullable reference types always enabled
- Pattern matching preferred over casting
- `ProcessStartInfo.ArgumentList` for git commands (never string interpolation into `Arguments`)
- `Math.Clamp` for all score calculations

## Balance Score Formula

```csharp
var alignment = Math.Clamp(1.0 - Math.Abs(strength - (1.0 - distance)), 0.0, 1.0);
var volatilityImpact = Math.Clamp(1.0 - Math.Clamp(volatility * strength, 0.0, 1.0), 0.0, 1.0);
var score = alignment * volatilityImpact;
```

## Testing

- xUnit for all tests
- Test naming: `MethodName_Scenario_ExpectedResult`
- `[Theory]` + `[InlineData]` for parameterized tests
- Golden file tests compare output against `tests/golden/*.txt`
- Fixture solutions live in `tests/fixtures/`

## Grade System

Grades are based on **issue density**, not average score:
- F: critical > 3
- D: critical > 0 OR highDensity > 0.05
- C: high > 0 OR mediumDensity > 0.25
- S: Over-optimized WARNING (not "excellent")
- A: mediumDensity <= 0.10
- B: Healthy (default)

## Project Structure (planned)

```
src/
  DotnetCoupling/           # Main CLI application
  DotnetCoupling.Core/      # Analysis engine (coupling, scoring, issues)
tests/
  DotnetCoupling.Tests/     # Unit tests
  DotnetCoupling.Integration.Tests/  # Integration tests
  fixtures/                 # Minimal .NET solutions for testing
  golden/                   # Expected output files
```

## Design Principles

1. **Precision over recall** — Don't guess coupling types; classify as Unknown if unsure
2. **Declare blind spots** — Syntax-only analysis has limits; state them explicitly
3. **Zero-dependency core** — Core library needs only `Microsoft.CodeAnalysis.CSharp`
4. **Sub-5s for 1000 files** — Parallelize Roslyn parsing and single-pass git log
