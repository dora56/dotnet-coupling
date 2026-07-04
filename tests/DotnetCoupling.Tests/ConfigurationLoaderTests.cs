using DotnetCoupling.Core;
using DotnetCoupling.Git;
using DotnetCoupling.Roslyn;
using System.Text.Json;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ExplicitConfig_ReadsThresholdsAndIgnores()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "analysis": {
                "exclude": ["**/Generated/**"],
                "testProjects": ["**/tests/**"]
              },
              "thresholds": {
                "maxDependencies": 3,
                "maxDependents": 4,
                "minTemporalCoupling": 2,
                "maxTemporalFilesPerCommit": 10,
                "scatteredExternalBreadth": 2
              },
              "ignore": {
                "paths": ["**/Legacy/**"],
                "namespaces": ["Sample.Legacy"],
                "issueTypes": ["GlobalComplexity"],
                "issues": [
                  {
                    "type": "CascadingChangeRisk",
                    "source": "Sample.Api.Handler",
                    "target": "Sample.Domain.Model",
                    "reason": "Tracked in ADR-001"
                  }
                ]
              }
            }
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        Assert.Equal(configPath, result.ConfigPath);
        Assert.Equal(3, result.Options.Thresholds.MaxDependencies);
        Assert.Equal(2, result.Options.Thresholds.MinTemporalCoupling);
        Assert.Contains("**/Generated/**", result.Options.ExcludePathPatterns);
        Assert.Contains("**/tests/**", result.Options.TestProjectPathPatterns);
        Assert.Contains("Sample.Legacy", result.Options.IgnoreNamespaces);
        Assert.Contains(IssueType.GlobalComplexity, result.Options.IgnoreIssueTypes);
        IssueSuppression suppression = Assert.Single(result.Options.IssueSuppressions);
        Assert.Equal(IssueType.CascadingChangeRisk, suppression.Type);
        Assert.Equal("Sample.Api.Handler", suppression.Source);
        Assert.Equal("Sample.Domain.Model", suppression.Target);
        Assert.Equal("Tracked in ADR-001", suppression.Reason);
    }

    [Fact]
    public void Load_AutoDiscovery_PrefersDotCouplingJsonOverCouplingJson()
    {
        string directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, "coupling.json"), """{ "thresholds": { "maxDependencies": 9 } }""");
        File.WriteAllText(Path.Combine(directory, ".coupling.json"), """{ "thresholds": { "maxDependencies": 7 } }""");

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, explicitConfig: null);

        Assert.Equal(7, result.Options.Thresholds.MaxDependencies);
    }

    [Fact]
    public void Load_ExplicitTomlConfig_ReadsThresholdsAndIgnores()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [analysis]
            exclude = ["**/Generated/**"]
            test_projects = ["**/tests/**"]

            [thresholds]
            max_dependencies = 3
            max_dependents = 4
            min_temporal_coupling = 2
            max_temporal_files_per_commit = 10
            scattered_external_breadth = 2

            [ignore]
            paths = ["**/Legacy/**"]
            namespaces = ["Sample.Legacy"]
            issue_types = ["GlobalComplexity"]

            [[ignore.issues]]
            type = "CascadingChangeRisk"
            source = "Sample.Api.Handler"
            target = "Sample.Domain.Model"
            reason = "Tracked in ADR-001"
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        Assert.Equal(configPath, result.ConfigPath);
        Assert.Equal(3, result.Options.Thresholds.MaxDependencies);
        Assert.Equal(2, result.Options.Thresholds.MinTemporalCoupling);
        Assert.Contains("**/Generated/**", result.Options.ExcludePathPatterns);
        Assert.Contains("**/tests/**", result.Options.TestProjectPathPatterns);
        Assert.Contains("Sample.Legacy", result.Options.IgnoreNamespaces);
        Assert.Contains(IssueType.GlobalComplexity, result.Options.IgnoreIssueTypes);
        IssueSuppression suppression = Assert.Single(result.Options.IssueSuppressions);
        Assert.Equal(IssueType.CascadingChangeRisk, suppression.Type);
        Assert.Equal("Sample.Api.Handler", suppression.Source);
        Assert.Equal("Sample.Domain.Model", suppression.Target);
        Assert.Equal("Tracked in ADR-001", suppression.Reason);
    }

    [Fact]
    public void Load_ExplicitConfig_ReadsDomainContext()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": [
                  {
                    "name": "Billing",
                    "category": "core",
                    "paths": ["src/Billing/**"],
                    "expectedVolatility": "high"
                  },
                  {
                    "name": "Reporting",
                    "category": "supporting",
                    "paths": ["src/Reporting/**"],
                    "expectedVolatility": "low"
                  },
                  {
                    "name": "IdentityProvider",
                    "category": "generic",
                    "paths": ["src/IdentityProvider/**"],
                    "expectedVolatility": "low"
                  }
                ]
              }
            }
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        DomainSubdomain billing = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Billing");
        Assert.Equal(SubdomainCategory.Core, billing.Category);
        Assert.Equal(Volatility.High, billing.ExpectedVolatility);
        Assert.Contains("src/Billing/**", billing.PathPatterns);
        DomainSubdomain reporting = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Reporting");
        Assert.Equal(SubdomainCategory.Supporting, reporting.Category);
        Assert.Equal(Volatility.Low, reporting.ExpectedVolatility);
        DomainSubdomain identityProvider = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "IdentityProvider");
        Assert.Equal(SubdomainCategory.Generic, identityProvider.Category);
        Assert.Equal(Volatility.Low, identityProvider.ExpectedVolatility);
    }

    [Fact]
    public void Load_ExplicitTomlConfig_ReadsDomainContext()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.subdomains]]
            name = "Billing"
            category = "core"
            paths = ["src/Billing/**"]
            expected_volatility = "high"

            [[domain.subdomains]]
            name = "Reporting"
            category = "supporting"
            paths = ["src/Reporting/**"]
            expected_volatility = "low"

            [[domain.subdomains]]
            name = "IdentityProvider"
            category = "generic"
            paths = ["src/IdentityProvider/**"]
            expected_volatility = "low"
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        DomainSubdomain billing = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Billing");
        Assert.Equal(SubdomainCategory.Core, billing.Category);
        Assert.Equal(Volatility.High, billing.ExpectedVolatility);
        Assert.Contains("src/Billing/**", billing.PathPatterns);
        DomainSubdomain reporting = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Reporting");
        Assert.Equal(SubdomainCategory.Supporting, reporting.Category);
        Assert.Equal(Volatility.Low, reporting.ExpectedVolatility);
        DomainSubdomain identityProvider = Assert.Single(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "IdentityProvider");
        Assert.Equal(SubdomainCategory.Generic, identityProvider.Category);
        Assert.Equal(Volatility.Low, identityProvider.ExpectedVolatility);
    }

    [Fact]
    public void Load_ExplicitConfig_ReadsDomainRolesAndAreas()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": [
                  {
                    "name": "Common",
                    "category": "supporting",
                    "paths": ["src/Common/**"],
                    "expectedVolatility": "low",
                    "strategicRole": "sharedKernel"
                  }
                ],
                "areas": [
                  {
                    "name": "Domain",
                    "paths": ["src/Common/Domain/**"],
                    "technicalRole": "domainModel"
                  },
                  {
                    "name": "Composition",
                    "paths": ["src/App/Program.cs"],
                    "technicalRole": "compositionRoot"
                  }
                ]
              }
            }
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        DomainSubdomain common = Assert.Single(result.Options.DomainContext.Subdomains);
        Assert.Equal(StrategicRole.SharedKernel, common.StrategicRole);
        DomainArea domain = Assert.Single(result.Options.DomainContext.Areas, area => area.Name == "Domain");
        Assert.Equal(TechnicalRole.DomainModel, domain.TechnicalRole);
        Assert.Contains("src/Common/Domain/**", domain.PathPatterns);
        DomainArea composition = Assert.Single(result.Options.DomainContext.Areas, area => area.Name == "Composition");
        Assert.Equal(TechnicalRole.CompositionRoot, composition.TechnicalRole);
    }

    [Fact]
    public void Load_ExplicitTomlConfig_ReadsDomainRolesAndAreas()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.subdomains]]
            name = "PublicApi"
            category = "supporting"
            paths = ["src/PublicApi/**"]
            expected_volatility = "medium"
            strategic_role = "open_host_service"

            [[domain.areas]]
            name = "Contracts"
            paths = ["src/PublicApi/Contracts/**"]
            technical_role = "contract"

            [[domain.areas]]
            name = "Adapters"
            paths = ["src/PublicApi/Endpoints/**"]
            technical_role = "adapter"
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, new FileInfo(configPath));

        DomainSubdomain publicApi = Assert.Single(result.Options.DomainContext.Subdomains);
        Assert.Equal(StrategicRole.OpenHostService, publicApi.StrategicRole);
        DomainArea contracts = Assert.Single(result.Options.DomainContext.Areas, area => area.Name == "Contracts");
        Assert.Equal(TechnicalRole.Contract, contracts.TechnicalRole);
        DomainArea adapters = Assert.Single(result.Options.DomainContext.Areas, area => area.Name == "Adapters");
        Assert.Equal(TechnicalRole.Adapter, adapters.TechnicalRole);
    }

    [Fact]
    public void Load_InvalidDomainCategory_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": [
                  {
                    "name": "Billing",
                    "category": "strategic",
                    "paths": ["src/Billing/**"],
                    "expectedVolatility": "high"
                  }
                ]
              }
            }
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid subdomain category", exception.Message);
        Assert.Contains("domain.subdomains[0].category", exception.Message);
    }

    [Fact]
    public void Load_DuplicateDomainSubdomainName_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": [
                  {
                    "name": "Reporting",
                    "category": "supporting",
                    "paths": ["src/Reporting/**"],
                    "expectedVolatility": "low"
                  },
                  {
                    "name": "reporting",
                    "category": "generic",
                    "paths": ["src/SharedReporting/**"],
                    "expectedVolatility": "low"
                  }
                ]
              }
            }
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Duplicate domain subdomain name", exception.Message);
        Assert.Contains("domain.subdomains[1].name", exception.Message);
        Assert.Contains("reporting", exception.Message);
    }

    [Fact]
    public void Load_DomainSubdomainWithoutPaths_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.subdomains]]
            name = "Reporting"
            category = "supporting"
            paths = []
            expected_volatility = "low"
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("domain.subdomains[0].paths", exception.Message);
    }

    [Fact]
    public void Load_InvalidDomainExpectedVolatility_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": [
                  {
                    "name": "Reporting",
                    "category": "supporting",
                    "paths": ["src/Reporting/**"],
                    "expectedVolatility": "veryHigh"
                  }
                ]
              }
            }
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid volatility", exception.Message);
        Assert.Contains("domain.subdomains[0].expectedVolatility", exception.Message);
    }

    [Fact]
    public void Load_DomainSubdomainWithoutExpectedVolatility_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.subdomains]]
            name = "Reporting"
            category = "supporting"
            paths = ["src/Reporting/**"]
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("domain.subdomains[0].expected_volatility", exception.Message);
    }

    [Fact]
    public void Load_DomainSubdomainsWrongShape_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "subdomains": {
                  "name": "Reporting"
                }
              }
            }
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("domain.subdomains must be an array", exception.Message);
    }

    [Fact]
    public void Load_InvalidStrategicRole_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.subdomains]]
            name = "Common"
            category = "supporting"
            paths = ["src/Common/**"]
            expected_volatility = "low"
            strategic_role = "core_domain"
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid strategic role", exception.Message);
        Assert.Contains("domain.subdomains[0].strategic_role", exception.Message);
    }

    [Fact]
    public void Load_InvalidTechnicalRole_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(
            configPath,
            """
            {
              "domain": {
                "areas": [
                  {
                    "name": "CoreLogic",
                    "paths": ["src/Core/**"],
                    "technicalRole": "coreLogic"
                  }
                ]
              }
            }
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid technical role", exception.Message);
        Assert.Contains("domain.areas[0].technicalRole", exception.Message);
    }

    [Fact]
    public void Load_DuplicateDomainAreaName_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.areas]]
            name = "Adapters"
            paths = ["src/Infrastructure/**"]
            technical_role = "adapter"

            [[domain.areas]]
            name = "adapters"
            paths = ["src/Api/**"]
            technical_role = "adapter"
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Duplicate domain area name", exception.Message);
        Assert.Contains("domain.areas[1].name", exception.Message);
    }

    [Fact]
    public void Load_DomainAreaWithoutPaths_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            [[domain.areas]]
            name = "Adapters"
            paths = []
            technical_role = "adapter"
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("domain.areas[0].paths", exception.Message);
    }

    [Fact]
    public void Load_DomainWrongShape_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(
            configPath,
            """
            domain = true
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("domain must be an object", exception.Message);
    }

    [Fact]
    public void Load_AutoDiscovery_UsesTomlWhenJsonDoesNotExist()
    {
        string directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, ".coupling.toml"), """
            [thresholds]
            max_dependencies = 7
            """);

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, explicitConfig: null);

        Assert.Equal(7, result.Options.Thresholds.MaxDependencies);
    }

    [Fact]
    public void Load_AutoDiscovery_PrefersJsonOverToml()
    {
        string directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, ".coupling.toml"), """
            [thresholds]
            max_dependencies = 3
            """);
        File.WriteAllText(Path.Combine(directory, ".coupling.json"), """{ "thresholds": { "maxDependencies": 7 } }""");

        ConfigurationLoadResult result = ConfigurationLoader.Load(directory, explicitConfig: null);

        Assert.Equal(7, result.Options.Thresholds.MaxDependencies);
    }

    [Fact]
    public void Load_TomlCamelCaseKey_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(configPath, """
            [thresholds]
            maxDependencies = 7
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Unknown configuration property", exception.Message);
        Assert.Contains("thresholds.maxDependencies", exception.Message);
    }

    [Fact]
    public void Load_InvalidToml_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.toml");
        File.WriteAllText(configPath, """
            [thresholds
            max_dependencies = 7
            """);

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid TOML configuration", exception.Message);
    }

    [Fact]
    public void Load_UnknownProperty_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(configPath, """{ "unexpected": true }""");

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Unknown configuration property", exception.Message);
    }

    [Fact]
    public void Load_InvalidIssueType_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(configPath, """{ "ignore": { "issueTypes": ["MadeUp"] } }""");

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("Invalid issue type", exception.Message);
    }

    [Fact]
    public void Load_InvalidSuppressedIssue_ThrowsConfigurationException()
    {
        string directory = CreateDirectory();
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(configPath, """{ "ignore": { "issues": [{ "type": "GlobalComplexity", "source": "Sample.Api.Handler", "target": "Sample.Infrastructure.Repository" }] } }""");

        ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
            ConfigurationLoader.Load(directory, new FileInfo(configPath)));

        Assert.Contains("ignore.issues[0].reason", exception.Message);
    }

    [Fact]
    public void Load_ExampleConfig_IsAccepted()
    {
        string configPath = Path.Combine(TestPaths.RepositoryRoot, ".coupling.example.json");

        ConfigurationLoadResult result = ConfigurationLoader.Load(TestPaths.RepositoryRoot, new FileInfo(configPath));

        Assert.Equal(20, result.Options.Thresholds.MaxDependencies);
        Assert.Contains("**/tests/**", result.Options.TestProjectPathPatterns);
        Assert.Contains(IssueType.ScatteredExternalCoupling, result.Options.IgnoreIssueTypes);
        Assert.Contains(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Billing");
        Assert.Contains(result.Options.DomainContext.Areas, area => area.TechnicalRole == TechnicalRole.DomainModel);
        Assert.Contains(result.Options.DomainContext.Areas, area => area.TechnicalRole == TechnicalRole.CompositionRoot);
    }

    [Fact]
    public void Load_ExampleTomlConfig_IsAccepted()
    {
        string configPath = Path.Combine(TestPaths.RepositoryRoot, ".coupling.example.toml");

        ConfigurationLoadResult result = ConfigurationLoader.Load(TestPaths.RepositoryRoot, new FileInfo(configPath));

        Assert.Equal(20, result.Options.Thresholds.MaxDependencies);
        Assert.Contains("**/tests/**", result.Options.TestProjectPathPatterns);
        Assert.Contains(IssueType.ScatteredExternalCoupling, result.Options.IgnoreIssueTypes);
        Assert.Contains(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Billing");
        Assert.Contains(result.Options.DomainContext.Subdomains, subdomain => subdomain.StrategicRole == StrategicRole.SharedKernel);
        Assert.Contains(result.Options.DomainContext.Areas, area => area.TechnicalRole == TechnicalRole.ApplicationService);
    }

    [Fact]
    public void Load_SelfTomlConfig_IsAccepted()
    {
        string configPath = Path.Combine(TestPaths.RepositoryRoot, ".coupling.toml");

        ConfigurationLoadResult result = ConfigurationLoader.Load(TestPaths.RepositoryRoot, new FileInfo(configPath));

        Assert.Equal(20, result.Options.Thresholds.MaxDependencies);
        Assert.Contains("tests/fixtures/**", result.Options.ExcludePathPatterns);
        Assert.Contains("**/tests/DotnetCoupling.Tests/**", result.Options.TestProjectPathPatterns);
        Assert.Contains(IssueType.ScatteredExternalCoupling, result.Options.IgnoreIssueTypes);
        Assert.Contains(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Core");
        Assert.Contains(result.Options.DomainContext.Subdomains, subdomain => subdomain.Name == "Roslyn");
        Assert.Contains(result.Options.DomainContext.Areas, area => area.Name == "CliCompositionRoot" && area.TechnicalRole == TechnicalRole.CompositionRoot);
        Assert.Contains(result.Options.DomainContext.Areas, area => area.Name == "CoreContracts" && area.TechnicalRole == TechnicalRole.Contract);
    }

    [Fact]
    public void ConfigSchema_IncludesAdvisoryRoleProperties()
    {
        string schemaPath = Path.Combine(TestPaths.RepositoryRoot, "schemas", "dotnet-coupling-config-0.2.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));

        JsonElement domainProperties = schema.RootElement
            .GetProperty("properties")
            .GetProperty("domain")
            .GetProperty("properties");
        JsonElement subdomainProperties = domainProperties
            .GetProperty("subdomains")
            .GetProperty("items")
            .GetProperty("properties");
        JsonElement areaProperties = domainProperties
            .GetProperty("areas")
            .GetProperty("items")
            .GetProperty("properties");

        string[] strategicRoles = subdomainProperties
            .GetProperty("strategicRole")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        string[] technicalRoles = areaProperties
            .GetProperty("technicalRole")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        Assert.Contains("sharedKernel", strategicRoles);
        Assert.Contains("openHostService", strategicRoles);
        Assert.Contains("domainModel", technicalRoles);
        Assert.Contains("compositionRoot", technicalRoles);
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
