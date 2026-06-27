# Issue Types Reference

Quick reference for AI agents implementing issue detection in `dotnet-coupling`.

## MVP Issues (cargo-coupling v0.3.3 aligned)

### GlobalComplexity
- **Trigger**: `strength >= 0.75 && distance >= 0.50`
- **Severity**: High (if score < 0.40), Medium otherwise
- **Meaning**: Strong coupling spanning a far distance
- **Fix**: Introduce interface, move closer, or add port/adapter

### CascadingChangeRisk
- **Trigger**: `strength >= 0.75 && volatility >= 0.75`
- **Severity**: High
- **Meaning**: Strong dependency on a frequently-changing target
- **Fix**: Stabilize the target's API, introduce interface, invert dependency

### InappropriateIntimacy
- **Trigger**: `strength == Intrusive && distance >= DifferentNamespace`
- **Severity**: High
- **Meaning**: Implementation-detail access across a boundary
- **C# markers**: public mutable field access, reflection, dynamic, service locator
- **Fix**: Encapsulate, use proper API, inject via interface

### HighEfferentCoupling
- **Trigger**: `outgoingDependencyCount > thresholds.maxDependencies` (default: 20)
- **Severity**: Medium (High if > 2× threshold)
- **Meaning**: A type/namespace depends on too many others
- **Fix**: Split responsibilities, extract sub-modules

### HighAfferentCoupling
- **Trigger**: `incomingDependentCount > thresholds.maxDependents` (default: 30)
- **Severity**: Medium (High if > 2× threshold)
- **Meaning**: Too many things depend on this component
- **Fix**: Stabilize public API, split shared model, add abstraction layer

### CircularDependency
- **Trigger**: Tarjan SCC size > 1 on namespace graph
- **Severity**: High (namespace level), Critical (project level in v0.2)
- **Meaning**: Mutual dependency cycle
- **Fix**: Invert one direction via interface, extract shared contract, use events

### HiddenCoupling
- **Trigger**: `coChangeCount >= minTemporalCoupling && no explicit code dependency`
- **Severity**: Medium (High if co-change is very strong)
- **Meaning**: Files always change together but have no visible coupling
- **Fix**: Extract shared concept, unify duplicated logic, make dependency explicit

### AccidentalVolatility
- **Trigger**: Supporting/generic subdomain file has High volatility
- **Severity**: Medium
- **Meaning**: Infrastructure or supporting code is churning like core domain
- **Fix**: Check for leaked domain logic, stabilize abstractions, review boundaries

### ScatteredExternalCoupling
- **Trigger**: `externalPackageDirectUsers >= thresholds.scatteredExternalBreadth` (default: 5)
- **Severity**: Medium
- **Meaning**: An external package is used directly in too many internal modules
- **Fix**: Introduce wrapper/adapter, centralize usage behind internal abstraction

## v0.2+ Issues (not in MVP)

| Type | Meaning |
|------|---------|
| UnnecessaryAbstraction | Over-abstraction of stable, close targets |
| GodType | Type with too many members |
| GodNamespace | Namespace with too many types |
| StaticUtilityHub | Static methods concentrated in one place |
| PrimitiveObsession | Many primitive parameters suggesting missing value types |
| ShallowModule | Module that adds no value, just passes through |
| PassThroughMethod | Method that delegates without logic |
| HighCognitiveLoad | Complex method/type requiring too much context |

## Severity Rules

| Condition | Severity |
|-----------|----------|
| Circular dependency at project boundary | Critical |
| Balance score < 0.20 | Critical |
| Balance score < 0.40 | High |
| Strong + far + high volatility | High |
| Efferent/Afferent threshold exceeded | Medium |
| Balance score < 0.60 | Medium |
| Minor smell | Low |

## Grade Calculation (issue density)

```
F: critical > 3
D: critical > 0 OR highDensity > 0.05
C: high > 0 OR mediumDensity > 0.25
S: mediumDensity <= 0.05 AND internalCouplings >= 20  (WARNING)
A: mediumDensity <= 0.10 AND internalCouplings >= 10
B: fallback
```

External couplings are excluded from density denominator.
