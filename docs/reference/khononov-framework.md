# Khononov Coupling Balance Framework

Reference for AI agents implementing `dotnet-coupling`.

Source: Vlad Khononov — "Balancing Coupling in Software Design"

## Core Principle

Coupling is not inherently bad. What matters is the **balance** between coupling strength, distance, and volatility.

## The Three Dimensions

### 1. Integration Strength

How tightly components depend on each other.

| Level | Score | Meaning | C# Example |
|-------|------:|---------|-------------|
| Contract | 0.25 | Depends on interface only | `IUserRepository`, generic constraint |
| Model | 0.50 | Depends on data structures | DTO, record, enum as parameter/return |
| Functional | 0.75 | Depends on behavior | `new ConcreteClass()`, method call |
| Intrusive | 1.00 | Depends on implementation details | public field access, reflection |

### 2. Distance

The physical or logical distance between dependent components.

| Level | Score | C# Example |
|-------|------:|-------------|
| SameNamespace | 0.25 | Same namespace, different type |
| DifferentNamespace | 0.50 | Same project, different namespace |
| DifferentProject | 0.75 | Same solution, different project |
| ExternalPackage | 1.00 | NuGet package or external assembly |

Note: Same-type references are excluded from analysis entirely.

### 3. Volatility

How frequently a component changes. Derived from git history.

| Level | Score | Change Count (6 months) |
|-------|------:|-------------------------|
| Low | 0.00 | 0–2 changes |
| Medium | 0.50 | 3–10 changes |
| High | 1.00 | 11+ changes |

DDD subdomain classification modifies interpretation:
- **Core** subdomains: high volatility is expected (essential volatility)
- **Supporting/Generic** subdomains: high volatility is suspicious (accidental volatility)

## The Balance Law

```
BALANCED = (STRENGTH XOR DISTANCE) OR NOT VOLATILITY
```

In plain terms:
- Strong coupling is acceptable when distance is close
- Weak coupling is acceptable at any distance
- High volatility amplifies the risk of strong coupling

## Numeric Formula

```
alignment = 1.0 - abs(strength - (1.0 - distance))
volatilityImpact = 1.0 - (volatility * strength)
score = alignment * volatilityImpact
```

All values are clamped to [0.0, 1.0].

## Design Decision Matrix

| Strength | Distance | Volatility | Verdict |
|----------|----------|------------|---------|
| Strong | Close | Low–Medium | ✅ OK — High cohesion |
| Weak | Far | Any | ✅ OK — Loose coupling |
| Strong | Far | Any | ⚠️ Needs improvement — Global complexity |
| Strong | Any | High | ⚠️ Needs improvement — Cascading change risk |
| Weak | Close | Low | 🤔 Consider — Possibly over-modularized |

## Key Insight: Grade is Issue Density, Not Average Score

A project with 1000 healthy couplings and 5 Critical issues is NOT healthy. Average score would hide the fire. Issue density catches it.

Grade S (Over-optimized) is a WARNING: possibly over-abstracted code where the architecture is consuming more effort than the product.
