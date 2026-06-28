# 配布設計

## 6. 配布設計

### 6.1 パッケージ名

| 項目 | 値 |
|---|---|
| NuGet PackageId | `dotnet-coupling` |
| ToolCommandName | `dotnet-coupling` |
| Repository | `https://github.com/<owner>/dotnet-coupling` |
| License | MIT |
| TargetFramework | `net10.0` |
| Runtime policy | .NET 10 以上。`<RollForward>Major</RollForward>` により .NET 11+ でも実行可能にする |

MVP から **`net10.0` を最低対象**にする。`net8.0` / `net9.0` への後方互換は持たない。理由は以下。

- 新規 dotnet tool として、最初から最新 LTS の API / SDK / tooling を前提にできる
- .NET 8 / 9 互換を背負うと、Roslyn / CLI / CI matrix の検証コストが増える
- `dotnet-coupling` はアプリに組み込む library ではなく開発支援 tool なので、利用者に .NET 10 以上の SDK / runtime を要求しやすい

将来 .NET 11 以降で動かすため、`<RollForward>Major</RollForward>` は設定する。ただし、ビルド対象 TFM は single target の `net10.0` とし、multi-target は採用しない。

### 6.2 csproj 設定

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RollForward>Major</RollForward>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>dotnet-coupling</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>

    <PackageId>dotnet-coupling</PackageId>
    <Version>0.3.0</Version>
    <Authors>YOUR_NAME</Authors>
    <Description>Experimental coupling balance analyzer for C#/.NET projects.</Description>
    <PackageTags>coupling;architecture;analysis;dotnet-tool;roslyn</PackageTags>
    <PackageProjectUrl>https://github.com/YOUR_GITHUB/dotnet-coupling</PackageProjectUrl>
    <RepositoryUrl>https://github.com/YOUR_GITHUB/dotnet-coupling</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.*" />
    <PackageReference Include="System.CommandLine" Version="2.*" />
  </ItemGroup>
</Project>
```

### 6.3 CLI parser

MVP では `System.CommandLine` を採用する。理由は以下。

- .NET CLI との親和性が高い
- help / version / validation / completion を標準的に扱える
- MVP のオプション数なら十分
- v0.2 以降に subcommand を増やしても破綻しにくい

`Cocona` や `Spectre.Console.Cli` は見栄えや体験面で魅力があるが、まずは依存を少なくする。`Spectre.Console` は表や色付き出力が欲しくなった時点で検討する。

### 6.4 pack

```bash
dotnet pack src/DotnetCoupling.Cli/DotnetCoupling.Cli.csproj -c Release
```

### 6.5 ローカルインストール検証

```bash
dotnet tool install --global dotnet-coupling \
  --add-source src/DotnetCoupling.Cli/nupkg
```

### 6.6 NuGet 公開

```bash
dotnet nuget push src/DotnetCoupling.Cli/nupkg/*.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```
