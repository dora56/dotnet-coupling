# Phase 5 Domain Context OSS Evaluation - 2026-07-05

This dogfood pass validates the Phase 5 Domain Context scoring calibration on
public OSS repositories. The goal is not to tune each repository perfectly, but
to check whether `domain.subdomains.expected_volatility` and
`domain.areas.technical_role` make the report more explainable without hiding
real architectural hotspots.

## Environment

- Tool commit: `1e5a755`
- Working directory: `/private/tmp/dotnet-coupling-phase5-oss-eval`
- Reused clones/config prototypes: `/private/tmp/dotnet-coupling-role-prototypes`
- Primary command shape:
  - `dotnet-coupling --json --hotspots 5 --no-git <repo-root>`
  - `dotnet-coupling --json --hotspots 5 --no-git --config <prototype.toml> <repo-root>`
  - `dotnet-coupling --json --hotspots 5 --no-git --config <prototype.toml> --mode semantic <repo-root-or-sln>`

`--no-git` was used to isolate the Domain Context calibration from observed
churn. This means `AccidentalVolatility` is not the focus of this pass.

## Results

| Target | Commit | Variant | Grade | Issues | Files | Components | Internal Couplings | Domain Match |
| --- | --- | --- | --- | --- | ---:| ---:| ---:| --- |
| Humanizer | `f9292aa` | no config syntax | C | C0 / H66 / M83 / L0 | 469 | 730 | 2198 | none |
| Humanizer | `f9292aa` | config syntax | S | C0 / H0 / M1 / L0 | 449 | 712 | 2193 | areas 350 matched / 53 unmatched |
| eShopOnWeb | `4da8212` | no config syntax | C | C0 / H7 / M115 / L0 | 248 | 250 | 468 | none |
| eShopOnWeb | `4da8212` | config syntax | D | C0 / H96 / M4 / L0 | 248 | 250 | 468 | subdomains 205 matched / 0 unmatched; areas 129 / 76 |
| eShopOnWeb | `4da8212` | config semantic explicit sln | D | C0 / H129 / M10 / L0 | 248 | 250 | 959 | 1 workspace diagnostic |
| CleanArchitecture | `4f80d58` | no config syntax | D | C0 / H58 / M169 / L0 | 355 | 380 | 550 | none |
| CleanArchitecture | `4f80d58` | config syntax | D | C0 / H37 / M3 / L0 | 92 | 92 | 117 | subdomains 74 / 0; areas 73 / 1 |
| CleanArchitecture | `4f80d58` | config semantic escalated | D | C0 / H59 / M4 / L0 | 89 | 89 | 225 | 1 workspace diagnostic |
| ModularMonolith | `91c8ef2` | no config syntax | C | C0 / H208 / M820 / L0 | 1062 | 1129 | 4195 | none |
| ModularMonolith | `91c8ef2` | config syntax | D | C0 / H405 / M349 / L0 | 1062 | 1129 | 4195 | subdomains 907 / 62; areas 923 / 46 |
| IDDD Samples | `914a746` | no config syntax | C | C0 / H9 / M73 / L0 | 269 | 271 | 1478 | none |
| IDDD Samples | `914a746` | config syntax | C | C0 / H52 / M47 / L0 | 269 | 271 | 1478 | subdomains 271 / 0; areas 271 / 0 |
| IDDD Samples | `914a746` | config semantic | C | C0 / H61 / M129 / L0 | 269 | 271 | 2767 | 15 workspace diagnostics |

## Interpretation

- Humanizer is the strongest positive signal. With a non-DDD, role-only config,
  test/benchmark noise disappears and the report collapses from `C / 149 issues`
  to `S / 1 medium issue`. The remaining hotspot is
  `Humanizer.GrammaticalGender` with high fan-in, which is plausible for a
  small public enum-like API. This supports keeping `technical_role` useful
  outside DDD codebases.
- CleanArchitecture improves in explainability. The prototype config removes
  sample/test noise and leaves the top hotspots in core/use-case/infrastructure
  code. Semantic mode finds more couplings, but still reports the same Grade D.
  The only remaining semantic diagnostic is Aspire AppHost related.
- eShopOnWeb shows the intended but sharp edge of `expected_volatility = high`.
  Marking `Storefront` as core raises `CascadingChangeRisk` and moves the report
  from C to D. Top hotspots are domain entities and web adapters, which is
  explainable, but the jump is large enough that config authors need guidance on
  when an entity, DTO, or shared model should be modeled as `contract`.
- ModularMonolith is the clearest over-calibration warning. The config increases
  High issues from 208 to 405 because high expected volatility is applied across
  large module domains, and module startup classes are currently configured as
  adapters rather than composition roots. This is useful feedback: the tool is
  sensitive enough, but role/config authoring needs better defaults and examples.
- IDDD Samples confirms the same DDD nuance. `TenantId` and application services
  remain high-priority hotspots. That is partly valid, but identity/value object
  concepts are closer to contracts than mutable domain behavior. The current
  role vocabulary can express this with `technical_role = contract`, but the
  docs/examples should make that path explicit.

## Semantic Mode Notes

- Humanizer semantic mode is blocked by `global.json` requiring
  `11.0.100-preview.3`.
- ModularMonolith semantic mode is blocked by `global.json` requiring SDK
  `8.0.0` with `rollForward = latestFeature` in this local environment.
- eShopOnWeb needs an explicit solution path because directory input is
  ambiguous. With `eShopOnWeb.sln`, it succeeds with one `.dcproj` workspace
  warning.
- CleanArchitecture needed an escalated rerun to avoid sandbox NuGet cache
  noise. After that, it succeeds with one Aspire AppHost diagnostic.
- IDDD Samples succeeds but reports expected legacy .NET Framework / Mono /
  packages diagnostics.
- `--mode auto` is not currently a supported CLI value. For OSS adoption, an
  explicit fallback mode may be worth adding later.

## Improvement Candidates

1. Add config authoring guidance for DDD value objects, identifiers, DTOs, and
   published language types: these often should be `technical_role = contract`
   even when they live under a core subdomain.
2. Add composition-root examples for module startup/configuration classes such
   as `*Startup`, `Program`, `*Module`, and infrastructure bootstrapping paths.
3. Consider a future `--mode auto` that tries semantic and falls back to syntax
   with diagnostics when SDK/workspace loading is blocked.
4. Add a report hint when Domain Context coverage is partial, especially when
   unmatched components remain in a configured repository.
5. Keep `expected_volatility = high` intentionally sharp, but document that it
   should be applied to genuinely volatile core behavior, not to every stable
   shared model under a core folder.

## Follow-up Closure

The Phase 5 close-out follow-up addresses items 1, 2, and 4:

- `.coupling.example.json` / `.coupling.example.toml`, README, and config design
  docs now show `contract` patterns for value objects, identifiers, DTOs,
  published language, and shared kernel contracts.
- The examples now show `composition_root` patterns for `Program`, `Startup`,
  module startup, DI registration, and host bootstrapping code.
- Summary output and JSON `manifest.runNotes` now include coverage hints when
  configured subdomains or technical roles leave components unmatched.

Items 3 and 5 remain future guidance rather than Phase 5 blockers. `--mode auto`
belongs to the default mode roadmap, and `expected_volatility = high` remains
intentionally sharp while the docs steer stable shared models toward `contract`.

## Artifact Paths

- `/private/tmp/dotnet-coupling-phase5-oss-eval/reports/Humanizer`
- `/private/tmp/dotnet-coupling-phase5-oss-eval/reports/eShopOnWeb`
- `/private/tmp/dotnet-coupling-phase5-oss-eval/reports/CleanArchitecture`
- `/private/tmp/dotnet-coupling-phase5-oss-eval/reports/ModularMonolith`
- `/private/tmp/dotnet-coupling-phase5-oss-eval/reports/IDDD`
