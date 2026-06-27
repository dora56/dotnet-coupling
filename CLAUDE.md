# dotnet-coupling

.NET CLI tool for coupling analysis based on Khononov's "Balancing Coupling in Software Design".

## Commands

```bash
# Development (all-in-one check)
dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes

# Run
dotnet run --project src/DotnetCoupling.Cli -- ./src                        # Analyze
dotnet run --project src/DotnetCoupling.Cli -- --summary ./src              # Summary only
dotnet run --project src/DotnetCoupling.Cli -- --json ./src                 # JSON output
dotnet run --project src/DotnetCoupling.Cli -- --check --min-grade B ./src  # CI gate
dotnet run --project src/DotnetCoupling.Cli -- --no-git ./src               # Skip git history

# Pack & local install
dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj -c Release
dotnet tool install --global dotnet-coupling --add-source src/DotnetCoupling.Cli/nupkg
dotnet tool uninstall --global dotnet-coupling

# Release
dotnet format --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release
dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj -c Release
# bump Version in .csproj, then:
git add -A
git commit -m "chore: release vX.Y.Z"
git tag -a vX.Y.Z -m "vX.Y.Z"
git push origin main
git push origin vX.Y.Z
# → GitHub Actions auto-publishes to NuGet.org
```

## Key Files

| File | Purpose |
|------|---------|
| `src/DotnetCoupling.Cli/Program.cs` | CLI entrypoint (System.CommandLine) |
| `src/DotnetCoupling.Cli/Analysis/Models.cs` | Core data types (Component, CouplingMetrics, BalanceScore, etc.) |
| `src/DotnetCoupling.Cli/Analysis/CSharpDependencyAnalyzer.cs` | Roslyn syntax-only dependency extraction |
| `src/DotnetCoupling.Cli/Analysis/GitVolatility.cs` | Git log parsing, volatility scoring, temporal co-change |
| `src/DotnetCoupling.Cli/Analysis/ReportRenderer.cs` | Text, summary, JSON output rendering |
| `dotnet-coupling-design.md` | Authoritative design specification |
| `tests/DotnetCoupling.Tests/` | xUnit test project |
| `tests/fixtures/` | C# fixture projects for integration tests |
| `tests/golden/` | Expected CLI outputs for golden tests |

## Docs & Rules

- `.agents/docs/` — Khononov framework reference, issue type catalog
- `.agents/rules/` — C# coding rules for this project
- `docs/design/` — Split design specification (13 files by concern)
- `docs/design/05-scoring.md` — Core algorithm (Strength/Distance/Volatility/Grade)
- `docs/design/06-issue-detection.md` — Issue types, severity, circular deps

## Design Principles

- **Issue density over average score** — Grade is determined by Critical/High/Medium issue density, not mean balance score
- **S grade is a warning** — "Over-optimized" means possibly over-abstracted, not excellent
- **Precision over recall** — Every false positive destroys trust in the tool. Prefer under-reporting
- **Declare blind spots** — A clean report must state what was NOT analyzed (the manifest)
- **Syntax-only confidence** — MVP uses Roslyn syntax trees only; output must declare this limitation
- **No network, no telemetry** — The tool reads source files and runs `git`. Nothing else

## Notes

Target framework: `net10.0` with `<RollForward>Major</RollForward>`.

Config: MVP reads `.coupling.json` only. TOML support is v0.2.

Git safety: Always use `ProcessStartInfo.ArgumentList` — never string-interpolate arguments.

External namespaces (`System.*`, `Microsoft.*`) are excluded from circular dependency detection and health grade density calculations.

Balance formula:
```
alignment = clamp(1.0 - abs(strength - (1.0 - distance)), 0.0, 1.0)
volatilityImpact = clamp(1.0 - clamp(volatility * strength, 0.0, 1.0), 0.0, 1.0)
score = clamp(alignment * volatilityImpact, 0.0, 1.0)
```
