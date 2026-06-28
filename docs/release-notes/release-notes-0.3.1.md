# dotnet-coupling 0.3.1

`0.3.1` is a patch release that hardens the release workflow and improves the
project's public entrypoint documentation without changing the CLI or JSON
contract introduced in `0.3.0`.

## Included

- expanded `README.md` with badges, quick start, CI usage, baseline guidance,
  and a clearer explanation of syntax vs semantic mode
- GitHub Actions artifact upload updated to `actions/upload-artifact@v7`
- local tool install steps in CI now read the package version from
  `Directory.Build.props` instead of hardcoding `0.3.0`
- a `self-benchmark` CI job that verifies the packed tool can analyze the
  repository with `--check --min-grade A --no-git`

## Compatibility Notes

- CLI options and behavior are unchanged from `0.3.0`
- JSON schema version remains unchanged
- `semantic` mode remains preview-only and must still be enabled explicitly

## Known Limitations

- semantic preview still depends on workspace prerequisites being loadable
- runtime DI container dependencies are not analyzed
- some reflection and `dynamic` flows remain unresolved or syntax-equivalent
- mutation remains part of CI quality gates rather than the release workflow
