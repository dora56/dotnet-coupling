# Phase 6 Investigation UX Dogfooding

## Scope

Phase 6 was exercised through a locally packed `0.6.0-preview.2` tool installed
under `/private/tmp/dotnet-coupling-phase6-dogfood-20260710/tool`.
The runs verify impact, semantic member trace, Markdown, AI handoff, Japanese
summary, and the invariant that selecting an investigation view does not change
Grade or issue counts.

| Target | Revision | Character |
|---|---|---|
| dotnet-coupling | branch `codex/phase6-investigation-ux`, base `9fa3baa` | self, `.coupling.toml` |
| Humanizer | `f9292aa90948de0aea2d4fa7d6549b1b2432c0fb` | non-DDD library |
| FluentValidation | `bae8916523805170e5dee6ee3e23c2937d2e2ec3` | non-DDD library |
| MediatR | `916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5` | non-DDD library |
| eShopOnWeb | `6d1019e53ba2d6ec96c2dd5bf7f944d5c8fe1180` | DDD sample |

## Self

Self runs always used `.coupling.toml`.

| Mode | Grade | High | Medium | Internal couplings |
|---|---:|---:|---:|---:|
| syntax directory | S | 0 | 0 | 1,213 |
| semantic solution | C | 14 | 12 | 5,209 |

The mode difference is pre-existing analysis sensitivity rather than an
investigation-output change. Semantic mode observes substantially more
dependencies and therefore remains opt-in.

- `--impact AnalysisReport --depth 3` resolved 22 dependents, 20 direct, across
  six projects; risk was `0.86875`.
- `--trace ReportRenderer.Render --depth 3` resolved six callers with source
  member and location evidence for direct callers.
- JSON investigation reports used schema `0.5`; Grade `C` and issue counts
  remained `0/14/12/0`.
- Markdown retained impact paths, issue recommendations, locations, and blind
  spots.
- AI output included configured complexity-assisted Top 10 candidates and
  bounded recommendations.
- Overloaded methods intentionally share a member identity, so tracing
  `ReportRenderer.Render` includes the delegating overload as a direct caller.

## OSS Impact

Syntax-mode impact used `--no-git` and repository `src` input.

| Target/query | Grade | High/Medium | Risk | Dependents/direct | Notes |
|---|---:|---:|---:|---:|---|
| Humanizer `GrammaticalGender` | S | 0/2 | 0.5250 | 66/42 | broad but coherent public enum blast radius |
| FluentValidation `AbstractValidator<T>` | C | 10/121 | 0.7425 | 121/55 | root `src` includes test projects, so test callers add noise |
| MediatR `Mediator` | D | 26/6 | 0.4975 | 4/1 | paths through DI configuration and registration |
| eShopOnWeb `Order` aggregate | C | 18/83 | 0.4350 | 4/3 | event, service, handler, and EF configuration paths were visible |

Each impact JSON report retained the same Grade and issue counts as its
corresponding syntax hotspot baseline.

## Semantic Trace

Production-project restore used an isolated NuGet cache under `/private/tmp`.

| Target/query | Syntax project | Semantic project | Callers | Direct evidence |
|---|---|---|---:|---|
| FluentValidation `IValidator.ValidateAsync` | C, 1H/54M | C, 8H/91M | 8 | extension, test helper, child validator adaptor |
| MediatR `RequestHandlerWrapper.Handle` | D, 26H/5M | C, 25H/17M | 5 | `Mediator.Send` at `Mediator.cs:82` |
| eShopOnWeb `Basket.AddItem` | C, 1H/10M | C, 4H/13M | 1 | `BasketService.AddItemToBasket` at `BasketService.cs:34` |

All three semantic project runs completed without workspace diagnostics.
Humanizer semantic restore was load-blocked because its current `global.json`
requires .NET SDK `11.0.100-preview.3`, while the dogfood environment provides
.NET 9 and .NET 10. Syntax impact and Markdown remained available.

## Output UX

- Humanizer Markdown made a 66-component blast radius inspectable, but very
  broad targets naturally produce long artifacts. JSON remains preferable for
  programmatic filtering.
- eShopOnWeb AI output surfaced the trace target, source location, active issue
  evidence, ranked candidates, existing recommendations, and verification
  constraints without external model calls.
- Humanizer `--summary --jp` localized Grade, file/type/coupling counts, issue
  counts, and the S-grade warning while retaining Git status.
- FluentValidation demonstrates that `analysis.test_projects` should be used
  when a repository places tests under `src`; impact intentionally preserves
  observed test dependents unless the analyzed scope excludes them.

## Findings And Remediation

Dogfooding exposed a real hotspot-ranking defect: a circular dependency issue
stored namespace paths as `"A -> B"`, and `HotspotAnalyzer` ranked that entire
string as a component. A characterization test now requires individual cycle
participants, and the implementation assigns the issue once to each namespace.
The corrected eShopOnWeb AI output ranks `BlazorShared.Interfaces` and
`BlazorShared.Models` separately and no longer emits pseudo-components.

No remaining Phase 6 blocker was found. The following limitations are explicit:

- overloads are aggregated by containing type and member name;
- transitive trace paths have source-member evidence only for the first
  observation, then component-level evidence;
- semantic viability depends on the target repository SDK and restore state;
- broad impact targets can create long Markdown artifacts.
