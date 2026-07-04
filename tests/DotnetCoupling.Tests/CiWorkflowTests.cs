using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CiWorkflowTests
{
    [Fact]
    public void ReportJob_WhenMutationJobFails_AttemptsMutationArtifactDownloadWithoutFailingSummary()
    {
        string workflowPath = Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "ci.yml");
        string workflow = File.ReadAllText(workflowPath);

        string step = ExtractStep(workflow, "Download mutation report");

        Assert.Contains("uses: actions/download-artifact@v8.0.1", step);
        Assert.Contains("name: mutation-report", step);
        Assert.Contains("continue-on-error: true", step);
        Assert.DoesNotContain("needs.mutation.result == 'success'", step);
        Assert.Contains("needs.mutation.result != 'skipped'", step);
        Assert.Contains("needs.mutation.result != 'cancelled'", step);
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
