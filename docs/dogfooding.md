# Dogfooding Guide

Use this guide to collect Phase 2 public alpha feedback.

## Self Dogfood

The `dogfood` GitHub Actions workflow runs weekly and on demand. It packs the
local tool, installs it from the generated package, and uploads:

- `dogfood-summary.txt`
- `dogfood-no-git-summary.txt`
- `dogfood-report.json`
- `dogfood-compare/semantic-compare.md`
- `dogfood-compare/syntax-summary.txt`
- `dogfood-compare/semantic-summary.txt`
- `dogfood-compare/syntax-report.json`
- `dogfood-compare/semantic-report.json`

The compare artifact runs against `dotnet-coupling.slnx` so that `syntax` and
`semantic-preview` can be inspected side by side on the same self-dogfood target.

## External Sample Dogfood

Run against 2-3 small C# repositories before publishing a new alpha.

Recommended target profile:

- small enough to inspect findings manually
- has normal Git history
- has at least two namespaces or projects
- does not require private package feeds to inspect source

Commands:

```bash
dotnet-coupling --summary ./src
dotnet-coupling --summary --no-git ./src
dotnet-coupling --json --no-git ./src > dotnet-coupling-report.json
dotnet-coupling --check --min-grade B ./src
```

Record for each target:

- repository URL and commit SHA
- command used
- grade and issue counts
- false positives with issue type and source/target
- false negatives with expected issue type
- confusing output or missing context

Use the GitHub issue templates for false positives and false negatives.
