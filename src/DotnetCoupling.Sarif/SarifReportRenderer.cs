using System.Reflection;
using DotnetCoupling.Core;
using Microsoft.CodeAnalysis.Sarif;

namespace DotnetCoupling.Sarif;

public static class SarifReportRenderer
{
    private static readonly string ToolVersion = typeof(SarifReportRenderer).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        .Split('+')[0]
        ?? typeof(SarifReportRenderer).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static string Render(AnalysisReport report, string repositoryRoot)
    {
        SarifLog log = CreateLog(report, repositoryRoot);
        using MemoryStream stream = new();
        log.Save(stream);
        stream.Position = 0;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().TrimEnd();
    }

    internal static SarifLog CreateLog(AnalysisReport report, string repositoryRoot)
    {
        CouplingIssue[] locatableIssues = report.Issues
            .Where(issue => issue.Location is not null)
            .ToArray();
        ReportingDescriptor[] rules = CreateRules(report.Issues);
        Dictionary<string, int> ruleIndexes = rules
            .Select((rule, index) => (rule.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);

        Run run = new()
        {
            Tool = new Tool
            {
                Driver = new ToolComponent
                {
                    Name = "dotnet-coupling",
                    SemanticVersion = ToolVersion,
                    InformationUri = new Uri("https://github.com/dora56/dotnet-coupling"),
                    Rules = rules,
                },
            },
            Invocations =
            [
                new Invocation
                {
                    ExecutionSuccessful = true,
                    WorkingDirectory = new ArtifactLocation
                    {
                        Uri = CreateDirectoryUri(repositoryRoot),
                    },
                },
            ],
            Results = locatableIssues
                .Select(issue => CreateResult(issue, repositoryRoot, ruleIndexes))
                .ToArray(),
        };

        return new SarifLog
        {
            SchemaUri = new Uri("https://json.schemastore.org/sarif-2.1.0.json"),
            Version = SarifVersion.Current,
            Runs = [run],
        };
    }

    private static ReportingDescriptor[] CreateRules(IReadOnlyList<CouplingIssue> issues)
    {
        return issues
            .GroupBy(issue => issue.Type)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group =>
            {
                CouplingIssue sample = group.First();
                return new ReportingDescriptor
                {
                    Id = sample.Type.ToString(),
                    Name = sample.Type.ToString(),
                    ShortDescription = new MultiformatMessageString
                    {
                        Text = sample.Problem,
                    },
                    FullDescription = new MultiformatMessageString
                    {
                        Text = sample.Problem,
                    },
                    Help = new MultiformatMessageString
                    {
                        Text = sample.Recommendation,
                        Markdown = sample.Recommendation,
                    },
                    DefaultConfiguration = new ReportingConfiguration
                    {
                        Level = ToFailureLevel(group.Max(issue => issue.Severity)),
                    },
                };
            })
            .ToArray();
    }

    private static Result CreateResult(
        CouplingIssue issue,
        string repositoryRoot,
        Dictionary<string, int> ruleIndexes)
    {
        SourceLocation location = issue.Location ?? throw new InvalidOperationException("SARIF results require a source location.");
        string relativePath = NormalizeRelativePath(repositoryRoot, location.File);
        string issueKey = $"{issue.Type}|{issue.Source}|{issue.Target}";

        return new Result
        {
            RuleId = issue.Type.ToString(),
            RuleIndex = ruleIndexes[issue.Type.ToString()],
            Level = ToFailureLevel(issue.Severity),
            Message = new Message
            {
                Text = issue.Problem,
            },
            Locations =
            [
                new Location
                {
                    PhysicalLocation = new PhysicalLocation
                    {
                        ArtifactLocation = new ArtifactLocation
                        {
                            Uri = new Uri(relativePath, UriKind.Relative),
                        },
                        Region = new Region
                        {
                            StartLine = Math.Max(1, location.Line),
                        },
                    },
                },
            ],
            PartialFingerprints = new Dictionary<string, string>
            {
                ["dotnetCouplingIssueKey"] = issueKey,
                ["dotnetCouplingSource"] = issue.Source,
                ["dotnetCouplingTarget"] = issue.Target,
            },
        };
    }

    private static Uri CreateDirectoryUri(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        return new Uri(fullPath);
    }

    private static string NormalizeRelativePath(string repositoryRoot, string filePath)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(repositoryRoot), Path.GetFullPath(filePath));
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static FailureLevel ToFailureLevel(Severity severity)
    {
        return severity switch
        {
            Severity.Critical or Severity.High => FailureLevel.Error,
            Severity.Medium => FailureLevel.Warning,
            Severity.Low => FailureLevel.Note,
            _ => FailureLevel.Warning,
        };
    }

}
