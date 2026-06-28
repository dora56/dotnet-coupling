using System.Diagnostics;
using DotnetCoupling.Cli;
using System.Text.Json;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_SummaryNoGit_ReturnsSummaryOutput()
    {
        CommandResult result = await RunCliAsync("--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Grade: C", result.Output);
        Assert.Contains("Git: disabled (--no-git)", result.Output);
        Assert.DoesNotContain("{", result.Output);
    }

    [Fact]
    public async Task RunAsync_JsonTakesPrecedenceOverSummaryAndCheck()
    {
        CommandResult result = await RunCliAsync("--json", "--summary", "--check", "--min-grade", "B", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("\"schemaVersion\": \"0.1\"", result.Output);
        Assert.StartsWith("{", result.Output.TrimStart(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CheckMinGrade_ReturnsFailureWhenGradeIsTooLow()
    {
        CommandResult result = await RunCliAsync("--check", "--min-grade", "B", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Grade: C", result.Output);
    }

    [Fact]
    public async Task RunAsync_FailOnMedium_ReturnsFailureWhenMediumIssueExists()
    {
        CommandResult result = await RunCliAsync("--check", "--fail-on", "Medium", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidFailOn_ReturnsCliArgumentError()
    {
        CommandResult result = await RunCliAsync("--check", "--fail-on", "Severe", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid severity", result.Error);
    }

    [Fact]
    public async Task RunAsync_MissingPath_ReturnsPathError()
    {
        CommandResult result = await RunCliAsync("--summary", Path.Combine(TestPaths.RepositoryRoot, "tests", "fixtures", "missing"));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Target path does not exist", result.Error);
    }

    [Fact]
    public async Task RunAsync_InvalidConfig_ReturnsCliArgumentError()
    {
        string directory = CreateDirectory();
        string sourcePath = Path.Combine(directory, "Sample.cs");
        string configPath = Path.Combine(directory, ".coupling.json");
        File.WriteAllText(sourcePath, "namespace Sample; public sealed class Type { }");
        File.WriteAllText(configPath, """{ "unexpected": true }""");

        CommandResult result = await RunCliAsync("--summary", "--config", configPath, "--no-git", directory);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown configuration property", result.Error);
    }

    [Fact]
    public async Task RunAsync_CsprojInput_ReturnsSummaryOutput()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(directory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--summary", "--no-git", projectPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
    }

    [Fact]
    public async Task RunAsync_InvalidCsprojInputInSyntaxMode_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(projectPath, "<Project><PropertyGroup>");
        WriteFile(
            Path.Combine(directory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--summary", "--no-git", projectPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_SlnxInputWithMissingListedProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string solutionPath = Path.Combine(directory, "Sample.slnx");
        Directory.CreateDirectory(appDirectory);
        WriteFile(
            solutionPath,
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Missing/Missing.csproj" />
            </Solution>
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_SlnInputWithMissingListedProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string solutionPath = Path.Combine(directory, "Sample.sln");
        Directory.CreateDirectory(appDirectory);
        WriteFile(
            solutionPath,
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "Missing\Missing.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Global
            EndGlobal
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSyntax_ReturnsSummaryOutput()
    {
        CommandResult result = await RunCliAsync("--mode", "syntax", "--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Grade: C", result.Output);
    }

    [Fact]
    public async Task RunAsync_DefaultSummary_MatchesExplicitSyntaxSummary()
    {
        CommandResult defaultResult = await RunCliAsync("--summary", "--no-git", TestPaths.Fixture("global-complexity"));
        CommandResult syntaxResult = await RunCliAsync("--mode", "syntax", "--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(0, defaultResult.ExitCode);
        Assert.Equal(defaultResult.Output, syntaxResult.Output);
        Assert.Equal(defaultResult.Error, syntaxResult.Error);
    }

    [Fact]
    public async Task RunAsync_InvalidMode_ReturnsCliArgumentError()
    {
        CommandResult result = await RunCliAsync("--mode", "future", "--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid value for --mode", result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemantic_ReturnsCliArgumentErrorUntilImplemented()
    {
        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", TestPaths.Fixture("global-complexity"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Semantic mode requires a .csproj, .sln, or .slnx input", result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticCsproj_ReturnsSummaryOutput()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(directory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", projectPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Mode: semantic-preview", result.Output);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticSummary_CharacterizesSemanticOnlyDynamicDispatchDifference()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(directory, "Handler.cs"),
            """
            using Sample.App.Infrastructure;

            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    dynamic repository = CreateRepository();
                    repository?.Save();
                }

                private static Repository CreateRepository()
                {
                    return new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(directory, "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
                public void Save()
                {
                }
            }
            """);

        CommandResult syntaxResult = await RunCliAsync("--mode", "syntax", "--summary", "--no-git", projectPath);
        CommandResult semanticResult = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", projectPath);

        Assert.Equal(0, syntaxResult.ExitCode);
        Assert.Contains("Files: 2 | Types: 2 | Couplings: 2 internal / 0 external", syntaxResult.Output);
        Assert.Contains("Issues: 0 Critical, 0 High, 1 Medium", syntaxResult.Output);
        Assert.DoesNotContain("Mode: semantic-preview", syntaxResult.Output, StringComparison.Ordinal);

        Assert.Equal(0, semanticResult.ExitCode);
        Assert.Contains("Files: 2 | Types: 2 | Couplings: 3 internal / 0 external", semanticResult.Output);
        Assert.Contains("Issues: 0 Critical, 1 High, 1 Medium", semanticResult.Output);
        Assert.Contains("Mode: semantic-preview", semanticResult.Output);
        Assert.Equal(string.Empty, syntaxResult.Error);
        Assert.Equal(string.Empty, semanticResult.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticJson_CharacterizesSemanticOnlyDynamicDispatchDifference()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(directory, "Handler.cs"),
            """
            using Sample.App.Infrastructure;

            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    dynamic repository = CreateRepository();
                    repository?.Save();
                }

                private static Repository CreateRepository()
                {
                    return new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(directory, "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
                public void Save()
                {
                }
            }
            """);

        CommandResult syntaxResult = await RunCliAsync("--mode", "syntax", "--json", "--no-git", projectPath);
        CommandResult semanticResult = await RunCliAsync("--mode", "semantic", "--json", "--no-git", projectPath);

        using JsonDocument syntaxDocument = JsonDocument.Parse(syntaxResult.Output);
        using JsonDocument semanticDocument = JsonDocument.Parse(semanticResult.Output);

        Assert.Equal(0, syntaxResult.ExitCode);
        Assert.Equal("syntax-only", syntaxDocument.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal(2, syntaxDocument.RootElement.GetProperty("analysis").GetProperty("couplings").GetProperty("internal").GetInt32());
        Assert.Equal(1, syntaxDocument.RootElement.GetProperty("issueCounts").GetProperty("medium").GetInt32());
        Assert.Equal(0, syntaxDocument.RootElement.GetProperty("issueCounts").GetProperty("high").GetInt32());

        Assert.Equal(0, semanticResult.ExitCode);
        Assert.Equal("semantic-preview", semanticDocument.RootElement.GetProperty("analysis").GetProperty("mode").GetString());
        Assert.Equal(3, semanticDocument.RootElement.GetProperty("analysis").GetProperty("couplings").GetProperty("internal").GetInt32());
        Assert.Equal(1, semanticDocument.RootElement.GetProperty("issueCounts").GetProperty("medium").GetInt32());
        Assert.Equal(1, semanticDocument.RootElement.GetProperty("issueCounts").GetProperty("high").GetInt32());
        Assert.Equal(string.Empty, syntaxResult.Error);
        Assert.Equal(string.Empty, semanticResult.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticSlnxWithInvalidProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string brokenDirectory = Path.Combine(directory, "Broken");
        string solutionPath = Path.Combine(directory, "Sample.slnx");
        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(brokenDirectory);
        WriteFile(
            solutionPath,
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Broken/Broken.csproj" />
            </Solution>
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(Path.Combine(brokenDirectory, "Broken.csproj"), "<Project><PropertyGroup>");
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Mode: semantic-preview", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticSlnWithInvalidProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string brokenDirectory = Path.Combine(directory, "Broken");
        string solutionPath = Path.Combine(directory, "Sample.sln");
        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(brokenDirectory);
        WriteFile(
            solutionPath,
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Broken", "Broken\Broken.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Global
            EndGlobal
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(Path.Combine(brokenDirectory, "Broken.csproj"), "<Project><PropertyGroup>");
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Mode: semantic-preview", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticSlnxWithMissingListedProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string solutionPath = Path.Combine(directory, "Sample.slnx");
        Directory.CreateDirectory(appDirectory);
        WriteFile(
            solutionPath,
            """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Missing/Missing.csproj" />
            </Solution>
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Mode: semantic-preview", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticSlnWithMissingListedProject_ContinuesWithRecoverableDiagnostic()
    {
        string directory = CreateDirectory();
        string appDirectory = Path.Combine(directory, "App");
        string solutionPath = Path.Combine(directory, "Sample.sln");
        Directory.CreateDirectory(appDirectory);
        WriteFile(
            solutionPath,
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "Missing\Missing.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Global
            EndGlobal
            """);
        WriteFile(
            Path.Combine(appDirectory, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(appDirectory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", solutionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Files: 1 | Types: 1", result.Output);
        Assert.Contains("Mode: semantic-preview", result.Output);
        Assert.Contains("Diagnostics: 1 recoverable warning(s)", result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task RunAsync_ModeSemanticLoadFailure_ReturnsStableWorkspaceError()
    {
        string directory = CreateDirectory();
        string projectPath = Path.Combine(directory, "Sample.App.csproj");
        WriteFile(
            Path.Combine(directory, "global.json"),
            """
            {
              "sdk": {
                "version": "0.0.1"
              }
            }
            """);
        WriteFile(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(
            Path.Combine(directory, "Sample.cs"),
            """
            namespace Sample.App;

            public sealed class Sample
            {
            }
            """);

        CommandResult result = await RunCliAsync("--mode", "semantic", "--summary", "--no-git", projectPath);

        Assert.Equal(4, result.ExitCode);
        Assert.Contains("Semantic workspace could not be loaded", result.Error);
        Assert.DoesNotContain("Unexpected analysis error", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Details:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BaselineCheckFailsWhenNewIssueMeetsFailOnThreshold()
    {
        string repository = CreateGitRepository();
        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
            }
            """);
        Commit(repository, "baseline");
        RunGit(repository, "branch", "baseline");

        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    _ = new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(repository, "src", "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
            }
            """);
        Commit(repository, "add new issue");

        CommandResult result = await RunCliAsync("--check", "--baseline", "baseline", "--fail-on", "Medium", "--no-git", Path.Combine(repository, "src"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Baseline: baseline", result.Output);
        Assert.Contains("New Issues: 0 Critical, 0 High, 1 Medium", result.Output);
    }

    [Fact]
    public async Task RunAsync_BaselineCheckPassesWhenOnlyExistingIssuesRemain()
    {
        string repository = CreateGitRepository();
        WriteFile(
            Path.Combine(repository, "src", "Api.cs"),
            """
            namespace Sample.App.Api;

            public sealed class Handler
            {
                public void Handle()
                {
                    _ = new Repository();
                }
            }
            """);
        WriteFile(
            Path.Combine(repository, "src", "Repository.cs"),
            """
            namespace Sample.App.Infrastructure;

            public sealed class Repository
            {
            }
            """);
        Commit(repository, "baseline");
        RunGit(repository, "branch", "baseline");
        File.AppendAllText(Path.Combine(repository, "README.md"), "change");
        Commit(repository, "non code change");

        CommandResult result = await RunCliAsync("--check", "--baseline", "baseline", "--no-git", Path.Combine(repository, "src"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Unchanged Issues: 1", result.Output);
    }

    private static async Task<CommandResult> RunCliAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exitCode = await CliApplication.RunAsync(args);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateGitRepository()
    {
        string repositoryPath = CreateDirectory();
        RunGit(repositoryPath, "init");
        RunGit(repositoryPath, "config", "user.email", "tests@example.invalid");
        RunGit(repositoryPath, "config", "user.name", "Dotnet Coupling Tests");
        RunGit(repositoryPath, "config", "commit.gpgsign", "false");
        RunGit(repositoryPath, "config", "tag.gpgsign", "false");
        return repositoryPath;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void Commit(string repositoryPath, string message)
    {
        RunGit(repositoryPath, "add", ".");
        RunGit(repositoryPath, "commit", "-m", message);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.\nSTDOUT:\n{standardOutput}\nSTDERR:\n{standardError}");
    }
}
