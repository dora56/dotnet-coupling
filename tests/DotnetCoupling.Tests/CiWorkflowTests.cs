using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CiWorkflowTests
{
    [Fact]
    public void CiWorkflow_DoesNotRunMutationInPullRequestFeedbackLoop()
    {
        string workflow = ReadCiWorkflow();
        string reportJob = ExtractJob(workflow, "report");

        Assert.DoesNotContain("\n  mutation:", workflow);
        Assert.DoesNotContain("- mutation", reportJob);
        Assert.DoesNotContain("Download mutation report", reportJob);
        Assert.Contains("--mutation-dir artifacts/mutation", reportJob);
    }

    [Fact]
    public void CouplingFeedbackJob_UploadsSarifOnlyForMainPushOrSameRepoPullRequest()
    {
        string workflow = ReadCiWorkflow();

        string job = ExtractJob(workflow, "coupling-feedback");

        Assert.Contains("permissions:", job);
        Assert.Contains("security-events: write", job);
        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --sarif --output dotnet-coupling.sarif --no-git ./src", job);
        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --hotspots 10 --no-git ./src", job);
        Assert.Contains("uses: github/codeql-action/upload-sarif@v4", job);
        Assert.Contains("github.event_name == 'push' || github.event.pull_request.head.repo.full_name == github.repository", job);
        Assert.Contains("name: dotnet-coupling-sarif", job);
        Assert.Contains("name: dotnet-coupling-hotspots", job);
    }

    [Fact]
    public void ReportJob_DownloadsCouplingFeedbackArtifactsWithoutFailingSummary()
    {
        string workflow = ReadCiWorkflow();

        string reportJob = ExtractJob(workflow, "report");
        string sarifStep = ExtractStep(reportJob, "Download coupling SARIF artifact");
        string hotspotsStep = ExtractStep(reportJob, "Download coupling hotspots artifact");

        Assert.Contains("- coupling-feedback", reportJob);
        Assert.Contains("name: dotnet-coupling-sarif", sarifStep);
        Assert.Contains("path: artifacts/coupling/sarif", sarifStep);
        Assert.Contains("continue-on-error: true", sarifStep);
        Assert.Contains("name: dotnet-coupling-hotspots", hotspotsStep);
        Assert.Contains("path: artifacts/coupling/hotspots", hotspotsStep);
        Assert.Contains("continue-on-error: true", hotspotsStep);
        Assert.Contains("--coupling-dir artifacts/coupling", reportJob);
    }

    [Fact]
    public void ReportJob_UsesPinnedOctocovWithCoverageRatchetsAndForkSafeCommenting()
    {
        string workflow = ReadCiWorkflow();
        string reportJob = ExtractJob(workflow, "report");
        string octocovConfig = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, ".octocov.yml"));

        Assert.Contains("actions: write", reportJob);
        Assert.Contains("pull-requests: write", reportJob);
        Assert.DoesNotContain("issues: write", reportJob);
        Assert.Contains("python3 scripts/generate-branch-coverage-metric.py", reportJob);
        Assert.Contains("OCTOCOV_CUSTOM_METRICS_BRANCH_COVERAGE", reportJob);
        Assert.Contains("uses: k1LoW/octocov-action@b3b6ee60482a667950f87553abf1df63217235d9", reportJob);
        Assert.Contains("version: v0.75.9", reportJob);
        Assert.Contains("if: always()", ExtractStep(reportJob, "Report coverage with octocov"));
        Assert.Contains("if: always()", ExtractStep(reportJob, "Generate CI summary"));
        Assert.Contains("OCTOCOV_FORK_PR", reportJob);
        Assert.Contains("current >= 90% && diff >= -0.1%", octocovConfig);
        Assert.Contains("artifact://${GITHUB_REPOSITORY}", octocovConfig);
        Assert.Contains("github.event_name == 'pull_request' && env.OCTOCOV_FORK_PR != 'true'", octocovConfig);
        Assert.Contains("summary:", octocovConfig);
        string workflowPermissions = workflow[..workflow.IndexOf("jobs:", StringComparison.Ordinal)];
        Assert.DoesNotContain("actions: write", workflowPermissions);
        Assert.DoesNotContain("pull-requests: write", workflowPermissions);
        Assert.Contains(
            "if: always() && github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name == github.repository",
            ExtractStep(reportJob, "Comment on PR"));
    }

    [Fact]
    public void SelfBenchmarkJob_GatesSemanticBaselineAndUploadsReport()
    {
        string workflow = ReadCiWorkflow();
        string job = ExtractJob(workflow, "self-benchmark");

        Assert.Contains("--check --min-grade A --no-git ./src", job);
        Assert.Contains("--baseline origin/main --fail-on High", job);
        Assert.Contains("--config .coupling.toml --mode semantic dotnet-coupling.slnx", job);
        Assert.Contains("semantic-self-report.json", job);
        Assert.Contains("name: semantic-self-report", job);
        Assert.Contains("if-no-files-found: error", job);
    }

    [Fact]
    public void NightlyMutationWorkflow_RunsFullStrykerAndUploadsReport()
    {
        string workflowPath = Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "nightly-mutation.yml");
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("name: nightly-mutation", workflow);
        Assert.Contains("schedule:", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("timeout-minutes: 90", workflow);
        Assert.Contains("config: stryker-config.json", workflow);
        Assert.Contains("config: stryker-complexity-config.json", workflow);
        Assert.Contains("dotnet tool run dotnet-stryker -- --config-file \"${{ matrix.config }}\"", workflow);
        Assert.DoesNotContain("--since", workflow);
        Assert.Contains("name: mutation-report-${{ matrix.name }}", workflow);
        Assert.Contains("path: StrykerOutput/**", workflow);
    }

    [Fact]
    public void DogfoodWorkflow_UsesSelfTomlConfigForReportsAndModeCompare()
    {
        string workflowPath = Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "dogfood.yml");
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --config .coupling.toml --summary ./src", workflow);
        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --config .coupling.toml --summary --no-git ./src", workflow);
        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --config .coupling.toml --json --no-git ./src", workflow);
        Assert.Contains("\"$TOOL_PATH/dotnet-coupling\" --config .coupling.toml --sarif --output dogfood-report.sarif --no-git ./src", workflow);
        Assert.Contains("scripts/generate-semantic-compare-report.sh", workflow);
        Assert.Contains("./dotnet-coupling.slnx", workflow);
        Assert.Contains(".coupling.toml", workflow);
    }

    [Fact]
    public void SemanticCompareScript_AcceptsOptionalConfigPathAndHighlightsSemanticDeltas()
    {
        string scriptPath = Path.Combine(TestPaths.RepositoryRoot, "scripts", "generate-semantic-compare-report.sh");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("[config-path]", script);
        Assert.Contains("config_args=(--config \"$config_path\")", script);
        Assert.Contains("\"${config_args[@]}\" \"$target_path\"", script);
        Assert.Contains("- Config: \\`", script);
        Assert.Contains("build_issue_type_delta_markdown()", script);
        Assert.Contains("## Issue Type Delta", script);
        Assert.Contains("build_semantic_only_high_issues_markdown()", script);
        Assert.Contains("issue_key(issue) not in syntax_keys", script);
        Assert.Contains("issue.get(\"severity\") in {\"Critical\", \"High\"}", script);
        Assert.Contains("## Semantic-Only High Issues Top 10", script);
    }

    private static string ReadCiWorkflow()
    {
        string workflowPath = Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "ci.yml");
        return File.ReadAllText(workflowPath);
    }

    private static string ExtractJob(string workflow, string jobName)
    {
        string marker = $"  {jobName}:";
        int start = workflow.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Job '{jobName}' was not found.");

        int searchStart = start + marker.Length;
        while (true)
        {
            int next = workflow.IndexOf("\n  ", searchStart, StringComparison.Ordinal);
            if (next < 0)
            {
                return workflow[start..];
            }

            int firstCharacterAfterJobIndent = next + 3;
            if (firstCharacterAfterJobIndent < workflow.Length && workflow[firstCharacterAfterJobIndent] != ' ')
            {
                return workflow[start..next];
            }

            searchStart = next + 1;
        }
    }

    private static string ExtractStep(string workflow, string stepName)
    {
        string marker = $"- name: {stepName}";
        int start = workflow.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Step '{stepName}' was not found.");

        int next = workflow.IndexOf("\n      - name:", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? workflow[start..] : workflow[start..next];
    }
}
