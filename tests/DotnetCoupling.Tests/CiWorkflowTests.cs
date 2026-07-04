using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CiWorkflowTests
{
    [Fact]
    public void ReportJob_WhenMutationJobFails_AttemptsMutationArtifactDownloadWithoutFailingSummary()
    {
        string workflow = ReadCiWorkflow();

        string step = ExtractStep(workflow, "Download mutation report");

        Assert.Contains("uses: actions/download-artifact@v8.0.1", step);
        Assert.Contains("name: mutation-report", step);
        Assert.Contains("continue-on-error: true", step);
        Assert.DoesNotContain("needs.mutation.result == 'success'", step);
        Assert.Contains("needs.mutation.result != 'skipped'", step);
        Assert.Contains("needs.mutation.result != 'cancelled'", step);
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
