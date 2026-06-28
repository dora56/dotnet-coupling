# Phase 2 OSS Dogfooding Debrief - 2026-06-28

## Summary

Overall: NEEDS_FIX_BEFORE_DEEPER_EVALUATION

Phase 2 dogfooding was run against three small-to-medium C# OSS repositories using
the published `dotnet-coupling 0.2.0-alpha.1` package. All three targets exposed
the same class of analyzer crash before reports could be produced.

This validates that public packaging and installation work, but it blocks deeper
false positive / false negative evaluation until analyzer type identity handles
common C# patterns such as partial types and generic/non-generic same-name types.

Tracked issue: https://github.com/dora56/dotnet-coupling/issues/8

Follow-up fixes and findings:

- https://github.com/dora56/dotnet-coupling/issues/10
- https://github.com/dora56/dotnet-coupling/issues/11

## Charter

```text
Explore Phase 2 public alpha dogfooding against small C# OSS repositories
Using the NuGet-published dotnet-coupling 0.2.0-alpha.1 tool and temporary clones under /private/tmp
To discover real-world crashes, false positives, false negatives, and rule-threshold gaps before Phase 3
```

## Environment

- Tool: `dotnet-coupling 0.2.0-alpha.1`
- Install path: `/private/tmp/dotnet-coupling-dogfood/tool`
- Source roots: `/private/tmp/dotnet-coupling-dogfood/*`
- Date: 2026-06-28

## Targets

| Repository | Scope | Result |
| --- | --- | --- |
| `ardalis/GuardClauses` | `src/GuardClauses` | Crash |
| `altmann/FluentResults` | `src/FluentResults` | Crash |
| `commandlineparser/commandline` | `src/CommandLine` | Crash |

## Findings

| Severity | Finding | Evidence |
| --- | --- | --- |
| Critical | Analyzer crashes on common duplicate type-key patterns. | Issue #8 |

### Duplicate Type-Key Crash

Observed commands:

```bash
/private/tmp/dotnet-coupling-dogfood/tool/dotnet-coupling --summary /private/tmp/dotnet-coupling-dogfood/GuardClauses/src/GuardClauses
/private/tmp/dotnet-coupling-dogfood/tool/dotnet-coupling --summary /private/tmp/dotnet-coupling-dogfood/FluentResults/src/FluentResults
/private/tmp/dotnet-coupling-dogfood/tool/dotnet-coupling --summary /private/tmp/dotnet-coupling-dogfood/commandline/src/CommandLine
```

Observed failures:

- GuardClauses: `An item with the same key has already been added. Key: Ardalis.GuardClauses.GuardClauseExtensions`
- FluentResults: `An item with the same key has already been added. Key: FluentResults.Result`
- commandline: `An item with the same key has already been added. Key: CSharpx.Either`

`--no-git` produced the same failure for sampled repos, so the failure is not
caused by Git volatility or Hidden Coupling detection.

Likely causes:

- Partial declarations are treated as separate types and inserted into a dictionary
  keyed only by fully qualified type name.
- Generic arity is not part of the syntax-only type key, so `Either<TLeft, TRight>`
  and `Either` collide.

## Coverage Notes

Completed:

- Public NuGet tool install smoke.
- Three OSS clone-and-run attempts under `/private/tmp`.
- Crash reproduction with and without Git.
- GitHub issue creation for the blocking defect.

Blocked by the crash:

- False positive / false negative classification.
- Scattered External Coupling real-world validation.
- Hidden Coupling commit-size and threshold validation.
- Baseline gate behavior on third-party repos.

## Recommended Follow-up

1. Fix analyzer type identity:
   - merge valid partial declarations into one logical type;
   - distinguish generic arity for non-partial same-name types.
2. Add fixture coverage for:
   - multi-file partial classes;
   - generic and non-generic same-name types in the same namespace.
3. Re-run the same three OSS targets and continue Phase 2 validation.
4. Only after reports are generated, classify false positives / false negatives and
   create targeted issues for rule or threshold problems.

## Follow-up Session

After fixing the duplicate type-key crash locally, the same three repositories were
re-run with a locally packed tool from the Phase 2 branch.

| Repository | Scope | Result |
| --- | --- | --- |
| `ardalis/GuardClauses` | `src/GuardClauses` | PASS, Grade C after Git path fix |
| `altmann/FluentResults` | `src/FluentResults` | PASS, Grade C after Git path fix |
| `commandlineparser/commandline` | `src/CommandLine` | PASS, Grade D after Git path fix |

### False Positive / False Negative Findings

| Issue | Type | Finding | Status |
| --- | --- | --- | --- |
| #10 | False negative | Hidden Coupling was missed when analyzing a Git subdirectory because git log paths were resolved against the target path instead of the repository root. | Fixed in branch |
| #11 | False positive / noise | Duplicate issue rows were emitted for the same issue type, source, and target. | Fixed in branch |

### External Namespace Exclusion

The OSS sample included 178 `System.*` / `Microsoft.*` using directives and
multiple internal namespace using directives such as `CommandLine.Core` and
`CommandLine.Infrastructure`.

Observed result:

- External coupling count stayed `0` for the sampled source scopes.
- No `ScatteredExternalCoupling` issue was emitted for `System.*`, `Microsoft.*`,
  or internal project namespaces.

This validates the Phase 2 external namespace exclusion behavior on real source.

### Hidden Coupling Threshold Validation

The OSS targets were re-run with `--git-months 120`.

Observed result after fixing Git root path resolution:

- `ardalis/GuardClauses`: 34 Hidden Coupling issues.
- `altmann/FluentResults`: 5 Hidden Coupling issues.
- `commandlineparser/commandline`: 35 Hidden Coupling issues at the default
  `minTemporalCoupling = 3`.
- `commandlineparser/commandline`: 288 Hidden Coupling issues at exploratory
  `minTemporalCoupling = 1`.
- Commits above `maxTemporalFilesPerCommit = 50` remain excluded by existing
  threshold tests.

The threshold behavior is visible and sensitive enough for dogfooding, but the
large jump at threshold `1` confirms that default Phase 2 settings should remain
conservative.
