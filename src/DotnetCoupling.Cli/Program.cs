using DotnetCoupling.Cli.Analysis;
using System.CommandLine;

RootCommand rootCommand = new("Analyze coupling balance in C#/.NET projects.");

Argument<string> pathArgument = new("path")
{
    Description = "Directory or C# file to analyze.",
    DefaultValueFactory = _ => ".",
};

Option<bool> summaryOption = new("--summary")
{
    Description = "Render a compact summary.",
};

Option<bool> jsonOption = new("--json")
{
    Description = "Render JSON output.",
};

Option<FileInfo?> outputOption = new("--output")
{
    Description = "Write the report to a file instead of stdout.",
};

Option<bool> checkOption = new("--check")
{
    Description = "Enable CI quality gate.",
};

Option<string> minGradeOption = new("--min-grade")
{
    Description = "Minimum acceptable grade for --check.",
    DefaultValueFactory = _ => "C",
};

Option<string?> failOnOption = new("--fail-on")
{
    Description = "Fail on the specified severity or higher.",
};

Option<bool> noGitOption = new("--no-git")
{
    Description = "Skip Git volatility analysis.",
};

Option<int> gitMonthsOption = new("--git-months")
{
    Description = "Number of months of Git history to inspect.",
    DefaultValueFactory = _ => 6,
};

Option<FileInfo?> configOption = new("--config")
{
    Description = "Configuration file path.",
};

rootCommand.Arguments.Add(pathArgument);
rootCommand.Options.Add(summaryOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(checkOption);
rootCommand.Options.Add(minGradeOption);
rootCommand.Options.Add(failOnOption);
rootCommand.Options.Add(noGitOption);
rootCommand.Options.Add(gitMonthsOption);
rootCommand.Options.Add(configOption);

rootCommand.SetAction(parseResult =>
{
    string targetPath = parseResult.GetValue(pathArgument) ?? ".";
    bool summary = parseResult.GetValue(summaryOption);
    bool json = parseResult.GetValue(jsonOption);
    FileInfo? output = parseResult.GetValue(outputOption);
    bool check = parseResult.GetValue(checkOption);
    string minGrade = parseResult.GetValue(minGradeOption) ?? "C";
    string? failOn = parseResult.GetValue(failOnOption);
    bool noGit = parseResult.GetValue(noGitOption);
    int gitMonths = parseResult.GetValue(gitMonthsOption);

    string fullTargetPath = Path.GetFullPath(targetPath);
    if (!Directory.Exists(fullTargetPath) && !File.Exists(fullTargetPath))
    {
        Console.Error.WriteLine($"Target path does not exist: {fullTargetPath}");
        return 3;
    }

    try
    {
        AnalysisReport report = CSharpDependencyAnalyzer.Analyze(fullTargetPath, !noGit, gitMonths);

        ReportFormat format = json
            ? ReportFormat.Json
            : summary || check
                ? ReportFormat.Summary
                : ReportFormat.Text;

        string rendered = ReportRenderer.Render(report, format);
        if (output is not null)
        {
            DirectoryInfo? outputDirectory = output.Directory;
            if (outputDirectory is not null && !outputDirectory.Exists)
            {
                outputDirectory.Create();
            }

            File.WriteAllText(output.FullName, rendered);
        }
        else
        {
            Console.WriteLine(rendered);
        }

        if (!check)
        {
            return 0;
        }

        if (GradeRank(report.Grade.Letter) > GradeRank(minGrade))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(failOn) && TryParseSeverity(failOn, out Severity threshold))
        {
            bool hasFailingSeverity = report.Issues.Any(issue => SeverityRank(issue.Severity) >= SeverityRank(threshold));
            return hasFailingSeverity ? 1 : 0;
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unexpected analysis error: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            Console.Error.WriteLine($"Details: {ex.StackTrace}");
        }
        return 4;
    }
});

return await rootCommand.Parse(args).InvokeAsync();

static int GradeRank(string grade)
{
    return grade.ToUpperInvariant() switch
    {
        "S" => 0,
        "A" => 1,
        "B" => 2,
        "C" => 3,
        "D" => 4,
        "F" => 5,
        _ => 3,
    };
}

static bool TryParseSeverity(string value, out Severity severity)
{
    return Enum.TryParse(value, ignoreCase: true, out severity);
}

static int SeverityRank(Severity severity)
{
    return severity switch
    {
        Severity.Low => 0,
        Severity.Medium => 1,
        Severity.High => 2,
        Severity.Critical => 3,
        _ => 0,
    };
}
