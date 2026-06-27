# Release Checklist: 0.2.0-alpha.1

Use this checklist after the repository remote and NuGet publishing secret are
available.

## Preconditions

- `origin` points to the GitHub repository.
- `NUGET_API_KEY` is configured as a GitHub Actions secret.
- The working tree is clean.
- The release commit contains `LICENSE`, release notes, config schema, report
  schema, and the Phase 2 baseline/config implementation.

## Local Gate

```bash
dotnet restore dotnet-coupling.slnx --locked-mode
dotnet build dotnet-coupling.slnx --configuration Release --no-restore
dotnet test dotnet-coupling.slnx --configuration Release --no-restore --no-build
dotnet format dotnet-coupling.slnx --verify-no-changes --no-restore
dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj --configuration Release --no-build
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
```

Install the generated package from the local source:

```bash
tool_path="$(mktemp -d)"
dotnet tool install dotnet-coupling \
  --tool-path "$tool_path" \
  --add-source src/DotnetCoupling.Cli/nupkg \
  --version 0.2.0-alpha.1
"$tool_path/dotnet-coupling" --summary --no-git ./src
```

## Release

```bash
git tag v0.2.0-alpha.1
git push origin main
git push origin v0.2.0-alpha.1
```

The `release` workflow must complete successfully and prove:

- cache restored or saved successfully
- locked restore passed
- build, test, format, and mutation gate passed
- package artifact `dotnet-coupling-nupkg` was uploaded
- mutation report artifact was uploaded
- local tool install smoke passed
- GitHub Release was created with `docs/release-notes-0.2.0-alpha.1.md`
- NuGet push completed or skipped as duplicate

## Post-Release Verification

```bash
dotnet tool uninstall --global dotnet-coupling || true
dotnet tool install --global dotnet-coupling --version 0.2.0-alpha.1
dotnet-coupling --summary --no-git ./src
dotnet-coupling --json --no-git ./src > /tmp/dotnet-coupling-0.2.0-alpha.1.json
```

Verify the JSON report:

- `tool` is `dotnet-coupling`
- `version` is `0.2.0-alpha.1`
- non-baseline output keeps `schemaVersion` `0.1`
- baseline output uses `schemaVersion` `0.2`

## Dogfood Evidence

Run the `dogfood` workflow manually after publish. Attach the uploaded dogfood
artifacts to any follow-up issues for false positives, false negatives, or UX
friction.
