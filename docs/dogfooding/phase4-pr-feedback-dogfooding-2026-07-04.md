# Phase 4 PR Feedback Dogfooding - 2026-07-04

Phase 4 dogfooding validates SARIF, Hotspots, and precise suppression behavior
before the `0.4.0` release candidate.

## Environment

- Tool package: `dotnet-coupling 0.4.0` installed from local `src/DotnetCoupling.Cli/nupkg`
- Working directory: `/private/tmp/dotnet-coupling-phase4-dogfood`
- Mode: default `syntax`, `--no-git` for deterministic PR-feedback smoke

## Results

| Target | Commit | Path | Grade | Issues | SARIF | Hotspots | Suppression smoke |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [self](https://github.com/dora56/dotnet-coupling) | `1ea2f28` | `/Users/r-hanaoka/Development/src/github.com/dora56/dotnet-coupling/src` | A | C0 / H0 / M12 / L0 | 7251 bytes | Top 5 generated | 1 suppressed; SARIF 6729 bytes |
| [MediatR](https://github.com/LuckyPennySoftware/MediatR) | `916ef1b` | `/private/tmp/dotnet-coupling-phase4-dogfood/repos/MediatR/src` | D | C0 / H26 / M6 / L0 | 18199 bytes | Top 5 generated | 1 suppressed; SARIF 17705 bytes |
| [FluentValidation](https://github.com/FluentValidation/FluentValidation) | `9439790` | `/private/tmp/dotnet-coupling-phase4-dogfood/repos/FluentValidation/src` | C | C0 / H9 / M121 / L0 | 64245 bytes | Top 5 generated | 1 suppressed; SARIF 63696 bytes |
| [Humanizer](https://github.com/Humanizr/Humanizer) | `f9292aa` | `/private/tmp/dotnet-coupling-phase4-dogfood/repos/Humanizer/src` | S | C0 / H0 / M2 / L0 | 1536 bytes | Top 3 generated | 1 suppressed; SARIF 734 bytes |

## Hotspots Top 5

### self

```text
Hotspots
------------------------------------------------------------
1. DotnetCoupling.Roslyn.CSharpDependencyAnalyzer
   Priority: 0.54
   Issues: 4 | Fan-in: 0 | Fan-out: 16 | Volatility: Low
   Reasons: 4 active issue(s), 0 incoming / 16 outgoing dependencies, crosses namespace or project boundary
2. DotnetCoupling.Roslyn.ComponentWalker
   Priority: 0.44
   Issues: 2 | Fan-in: 1 | Fan-out: 6 | Volatility: Low
   Reasons: 2 active issue(s), 1 incoming / 6 outgoing dependencies, crosses namespace or project boundary
3. DotnetCoupling.Git.GitVolatilityProvider
   Priority: 0.42
   Issues: 2 | Fan-in: 1 | Fan-out: 3 | Volatility: Low
   Reasons: 2 active issue(s), 1 incoming / 3 outgoing dependencies, crosses namespace or project boundary
4. DotnetCoupling.Core.AnalysisDiagnostic
   Priority: 0.41
   Issues: 2 | Fan-in: 2 | Fan-out: 0 | Volatility: Low
   Reasons: 2 active issue(s), 2 incoming / 0 outgoing dependencies, crosses namespace or project boundary
```

### MediatR

```text
Hotspots
------------------------------------------------------------
1. MediatR.Registration.ServiceRegistrar
   Priority: 0.77
   Issues: 13 | Fan-in: 0 | Fan-out: 21 | Volatility: Low
   Reasons: 13 active issue(s), High severity issue, 0 incoming / 21 outgoing dependencies, crosses namespace or project boundary
2. MediatR.Mediator
   Priority: 0.74
   Issues: 11 | Fan-in: 1 | Fan-out: 12 | Volatility: Low
   Reasons: 11 active issue(s), High severity issue, 1 incoming / 12 outgoing dependencies, crosses namespace or project boundary
3. Microsoft.Extensions.DependencyInjection.MediatRServiceConfiguration
   Priority: 0.73
   Issues: 5 | Fan-in: 3 | Fan-out: 8 | Volatility: Low
   Reasons: 5 active issue(s), High severity issue, 3 incoming / 8 outgoing dependencies, crosses namespace or project boundary
4. Microsoft.Extensions.DependencyInjection.MediatRServiceCollectionExtensions
   Priority: 0.66
   Issues: 4 | Fan-in: 0 | Fan-out: 3 | Volatility: Low
   Reasons: 4 active issue(s), High severity issue, 0 incoming / 3 outgoing dependencies, crosses namespace or project boundary
```

### FluentValidation

```text
Hotspots
------------------------------------------------------------
1. FluentValidation.InlineValidator`1
   Priority: 0.77
   Issues: 28 | Fan-in: 35 | Fan-out: 1 | Volatility: Low
   Reasons: 28 active issue(s), High severity issue, 35 incoming / 1 outgoing dependencies, crosses namespace or project boundary
2. FluentValidation.AbstractValidator`1
   Priority: 0.77
   Issues: 7 | Fan-in: 55 | Fan-out: 19 | Volatility: Low
   Reasons: 7 active issue(s), High severity issue, 55 incoming / 19 outgoing dependencies, crosses namespace or project boundary
3. FluentValidation.Tests.ValidatorTesterTester
   Priority: 0.75
   Issues: 6 | Fan-in: 0 | Fan-out: 16 | Volatility: Low
   Reasons: 6 active issue(s), High severity issue, 0 incoming / 16 outgoing dependencies, crosses namespace or project boundary
4. FluentValidation.Tests.LanguageManagerTests
   Priority: 0.66
   Issues: 3 | Fan-in: 0 | Fan-out: 9 | Volatility: Low
   Reasons: 3 active issue(s), High severity issue, 0 incoming / 9 outgoing dependencies, crosses namespace or project boundary
```

### Humanizer

```text
Hotspots
------------------------------------------------------------
1. Humanizer.FormatterRegistry
   Priority: 0.39
   Issues: 1 | Fan-in: 2 | Fan-out: 2 | Volatility: Low
   Reasons: 1 active issue(s), 2 incoming / 2 outgoing dependencies, crosses namespace or project boundary
2. Benchmarks.FormatterBenchmarks
   Priority: 0.38
   Issues: 1 | Fan-in: 0 | Fan-out: 2 | Volatility: Low
   Reasons: 1 active issue(s), 0 incoming / 2 outgoing dependencies, crosses namespace or project boundary
3. Humanizer.GrammaticalGender
   Priority: 0.25
   Issues: 1 | Fan-in: 42 | Fan-out: 0 | Volatility: Low
   Reasons: 1 active issue(s), 42 incoming / 0 outgoing dependencies
```

## Suppression Smoke

- self: suppressed `GlobalComplexity` from `DotnetCoupling.Cli.CliApplication` to `DotnetCoupling.Git.GitVolatilityProvider`; JSON reported 1 suppressed issue, and SARIF generation completed.
- MediatR: suppressed `GlobalComplexity` from `MediatR.Mediator` to `MediatR.NotificationPublishers.ForeachAwaitPublisher`; JSON reported 1 suppressed issue, and SARIF generation completed.
- FluentValidation: suppressed `GlobalComplexity` from `FluentValidation.Tests.AbstractValidatorTester` to ``FluentValidation.InlineValidator`1``; JSON reported 1 suppressed issue, and SARIF generation completed.
- Humanizer: suppressed `GlobalComplexity` from `Benchmarks.FormatterBenchmarks` to `Humanizer.FormatterRegistry`; JSON reported 1 suppressed issue, and SARIF generation completed.

## Notes

- SARIF files were generated by the CLI `--sarif` path and are stored under `/private/tmp/dotnet-coupling-phase4-dogfood/reports/*/report.sarif`.
- Hotspots are text artifacts from `--hotspots 5`; JSON hotspots were generated through `--json --hotspots 5`.
- Code Scanning display requires GitHub repository-side upload execution; local dogfood validates the SARIF artifact generation path.
