# dotnet-coupling 0.1.0-alpha.1

`0.1.0-alpha.1` is the first public alpha of `dotnet-coupling`.

## Included

- Syntax-only C# coupling analysis.
- Git volatility and temporal co-change analysis.
- Text, summary, and JSON output.
- JSON report schema version `0.1`.
- CI check mode with `--check`, `--min-grade`, and `--fail-on`.

## Known Limitations

- Semantic symbol resolution is not enabled.
- `.slnx`, `.sln`, and `.csproj` workspace loading is not enabled.
- Runtime DI container resolution is not analyzed.
- Reflection and `dynamic` calls may be incomplete.
- Generated code is excluded by default.
- Config and baseline support are Phase 2 capabilities for `0.2.0-alpha.1`.

## Blind Spots

The report manifest includes syntax-only blind spots so consumers can distinguish
alpha findings from stable semantic analysis.
