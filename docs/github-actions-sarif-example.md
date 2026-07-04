# GitHub Actions SARIF Example

Use this when a repository wants dotnet-coupling issues to appear in GitHub
Code Scanning while keeping the CLI gate behavior unchanged.

```yaml
name: coupling-sarif

on:
  pull_request:
  push:
    branches:
      - main

permissions:
  contents: read
  security-events: write

jobs:
  dotnet-coupling:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.x"

      - name: Install dotnet-coupling
        run: dotnet tool install --global dotnet-coupling --version 0.4.0

      - name: Generate SARIF
        run: dotnet-coupling --sarif --output dotnet-coupling.sarif ./src

      - name: Upload SARIF
        if: github.event_name == 'push' || github.event.pull_request.head.repo.full_name == github.repository
        uses: github/codeql-action/upload-sarif@v4
        with:
          sarif_file: dotnet-coupling.sarif

      - name: Upload SARIF artifact
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: dotnet-coupling-sarif
          path: dotnet-coupling.sarif
          if-no-files-found: error
```

Fork pull requests usually do not have permission to upload Code Scanning
alerts. Keep the artifact step so reviewers can still inspect the SARIF output.
