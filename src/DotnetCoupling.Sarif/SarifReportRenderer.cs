using System.Reflection;
using DotnetCoupling.Core;
using Microsoft.CodeAnalysis.Sarif;

namespace DotnetCoupling.Sarif;

public static class SarifReportRenderer
{
    private const string ToolName = "dotnet-coupling";
    private const string ToolInformationUri = "https://github.com/dora56/dotnet-coupling";
    private const string SarifSchemaUri = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string OmittedIssueCountProperty = "dotnetCouplingOmittedIssueCount";
    private const string IssueKeyFingerprint = "dotnetCouplingIssueKey";
    private const string SourceFingerprint = "dotnetCouplingSource";
    private const string TargetFingerprint = "dotnetCouplingTarget";

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
        int omittedIssueCount = report.Issues.Count - locatableIssues.Length;
        ReportingDescriptor[] rules = CreateRules(report.Issues);
        Dictionary<string, int> ruleIndexes = CreateRuleIndexes(rules);
        Run run = CreateRun(rules, locatableIssues, repositoryRoot, ruleIndexes);
        AddOmittedIssueCount(run, omittedIssueCount);

        return new SarifLog
        {
            SchemaUri = new Uri(SarifSchemaUri),
            Version = SarifVersion.Current,
            Runs = [run],
        };
    }

    private static Run CreateRun(
        ReportingDescriptor[] rules,
        CouplingIssue[] locatableIssues,
        string repositoryRoot,
        IReadOnlyDictionary<string, int> ruleIndexes)
    {
        return new Run
        {
            Tool = new Tool
            {
                Driver = new ToolComponent
                {
                    Name = ToolName,
                    SemanticVersion = ToolVersion,
                    InformationUri = new Uri(ToolInformationUri),
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
    }

    private static Dictionary<string, int> CreateRuleIndexes(ReportingDescriptor[] rules)
    {
        return rules
            .Select((rule, index) => (rule.Id, Index: index))
            .ToDictionary(pair => pair.Id, pair => pair.Index, StringComparer.Ordinal);
    }

    private static void AddOmittedIssueCount(Run run, int omittedIssueCount)
    {
        if (omittedIssueCount > 0)
        {
            run.SetProperty(OmittedIssueCountProperty, omittedIssueCount);
        }
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
        IReadOnlyDictionary<string, int> ruleIndexes)
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
                [IssueKeyFingerprint] = issueKey,
                [SourceFingerprint] = issue.Source,
                [TargetFingerprint] = issue.Target,
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
