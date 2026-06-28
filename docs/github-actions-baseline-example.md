# GitHub Actions Baseline Gate Example

Use this example when a repository wants to block only newly introduced coupling
issues while leaving existing debt visible.

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
        run: dotnet tool install --global dotnet-coupling --version 0.3.1

      - name: Coupling baseline gate
        run: dotnet-coupling --check --baseline origin/main --fail-on High ./src
```

The baseline gate compares issue keys by `(issue_type, source, target)`. Line
numbers are not part of the key.
