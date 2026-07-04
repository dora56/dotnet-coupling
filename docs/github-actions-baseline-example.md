# GitHub Actions Baseline Gate Example

Use this example when a repository wants to block only newly introduced coupling
issues while leaving existing debt visible. In `0.4.0`, the baseline is a Git
ref comparison rather than a generated baseline file.

```yaml
name: coupling

on:
  pull_request:

jobs:
  dotnet-coupling:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.x"

      - name: Install dotnet-coupling
        run: dotnet tool install --global dotnet-coupling --version 0.4.0

      - name: Coupling baseline gate
        run: dotnet-coupling --check --baseline origin/main --fail-on High ./src
```

The baseline gate compares issue keys by `(issue_type, source, target)`. Line
numbers are not part of the key.

## Handling Accepted Existing Debt

Use the moving branch ref as the ratchet baseline, and suppress only issues that
the team has explicitly accepted. Suppressions live in `.coupling.json`:

```json
{
  "ignore": {
    "issues": [
      {
        "type": "GlobalComplexity",
        "source": "Sample.App.Api.LegacyHandler",
        "target": "Sample.App.Infrastructure.LegacyRepository",
        "reason": "Accepted legacy dependency tracked in ARCH-42"
      }
    ]
  }
}
```

Suppressed issues are excluded from active issue counts, grade calculation,
`--check`, and SARIF upload. They remain visible in summary and JSON output so
the accepted debt does not disappear from review.

Recommended update workflow:

1. Run `dotnet-coupling --summary --baseline origin/main ./src` on a pull request.
2. Fix newly introduced High or Critical issues before merging.
3. For intentionally accepted long-lived debt, add a precise `ignore.issues`
   entry with a ticket or ADR in `reason`.
4. Review suppressions periodically and remove entries once the issue is fixed.
