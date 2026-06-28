# Phase 3 Synthetic Semantic Diff Characterization - 2026-06-28

## Summary

Overall: SEMANTIC_PREVIEW_DIFF_IS_INTENTIONAL_AND_USER_VISIBLE

`dynamic repository = CreateRepository(); repository?.Save();` という
小さな synthetic target を使って、`syntax` と `semantic-preview` の差分が
どのレイヤでどう見えるかを固定した。

Observed pattern:

- `syntax` でも object creation / return type 由来で `2 internal couplings`
- `semantic-preview` では dynamic dispatch が 1 本増えて `3 internal couplings`
- その増分により `High issue` が 1 件増える
- この差分は unit / renderer JSON / CLI summary / CLI JSON の各断面で
  一貫して観測できる

これは、Phase 3b の semantic precision が単なる内部実装差分ではなく、
ユーザーが見る出力まで含めて意図的な差分として表現されていることを示す。

## Target Shape

Target fixture:

```csharp
using Sample.App.Infrastructure;

namespace Sample.App.Api;

public sealed class Handler
{
    public void Handle()
    {
        dynamic repository = CreateRepository();
        repository?.Save();
    }

    private static Repository CreateRepository()
    {
        return new Repository();
    }
}
```

Supporting type:

```csharp
namespace Sample.App.Infrastructure;

public sealed class Repository
{
    public void Save()
    {
    }
}
```

## Characterized Output

### Summary / CLI

| Mode | Couplings | Issues | Note |
| --- | --- | --- | --- |
| `syntax` | `2 internal / 0 external` | `0 Critical, 0 High, 1 Medium` | dynamic dispatch not resolved |
| `semantic-preview` | `3 internal / 0 external` | `0 Critical, 1 High, 1 Medium` | dynamic dispatch resolved |

### JSON

| Mode | `analysis.mode` | `analysis.couplings.internal` | `issueCounts.high` |
| --- | --- | --- | --- |
| `syntax` | `syntax-only` | `2` | `0` |
| `semantic-preview` | `semantic-preview` | `3` | `1` |

## Why This Matters

Real OSS/self-dogfood targets often show either:

- no headline delta, because current thresholds absorb the extra couplings, or
- large coupling-count increases whose root cause is harder to isolate quickly

This synthetic target gives Phase 3c a stable explanation sample:

1. a case where semantic resolution adds exactly one meaningful coupling
2. a case where that added coupling changes issue severity output
3. a case small enough that reviewers can reason about the diff without
   reverse-engineering a large repository

## Verification Anchors

These automated checks now cover the characterization:

- `CSharpDependencyAnalyzerTests`
  - semantic-only dynamic factory-return inference
  - conditional-access dynamic dispatch
- `ReportRendererTests`
  - `Render_JsonOutput_SyntaxAndSemanticPreview_CharacterizeSemanticOnlyDynamicDispatchDiff`
- `CliApplicationTests`
  - `RunAsync_ModeSemanticSummary_CharacterizesSemanticOnlyDynamicDispatchDifference`
  - `RunAsync_ModeSemanticJson_CharacterizesSemanticOnlyDynamicDispatchDifference`

## Implication for Phase 3c

This does not replace OSS dogfooding. It complements it:

- OSS compare answers "does semantic preview stay stable on real repositories?"
- this synthetic compare answers "when semantic preview does differ, is the
  difference intentional, explainable, and visible through supported outputs?"
