using System.Diagnostics;
using System.IO.Compression;

namespace DotnetCoupling.Git;

public sealed class BaselineWorkspace : IDisposable
{
    private readonly string _temporaryDirectory;

    private BaselineWorkspace(string temporaryDirectory, string extractedTargetPath)
    {
        _temporaryDirectory = temporaryDirectory;
        TargetPath = extractedTargetPath;
    }

    public string TargetPath { get; }

    public static BaselineWorkspace Create(string repositoryRoot, string currentTargetPath, string baselineRef)
    {
        string fullRepositoryRoot = Path.GetFullPath(repositoryRoot);
        string fullTargetPath = Path.GetFullPath(currentTargetPath);
        string relativeTargetPath = Path.GetRelativePath(fullRepositoryRoot, fullTargetPath);
        if (relativeTargetPath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeTargetPath))
        {
            throw new BaselineException("Baseline target must be inside the Git repository.");
        }

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "dotnet-coupling-baseline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        string archivePath = Path.Combine(temporaryDirectory, "baseline.zip");
        RunGitArchive(fullRepositoryRoot, baselineRef, archivePath);
        ZipFile.ExtractToDirectory(archivePath, temporaryDirectory);

        string extractedTargetPath = Path.Combine(temporaryDirectory, relativeTargetPath);
        if (!Directory.Exists(extractedTargetPath) && !File.Exists(extractedTargetPath))
        {
            throw new BaselineException($"Baseline ref '{baselineRef}' does not contain target path '{relativeTargetPath}'.");
        }

        return new BaselineWorkspace(temporaryDirectory, extractedTargetPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch
        {
            /* best-effort cleanup */
        }
    }

    private static void RunGitArchive(string repositoryRoot, string baselineRef, string archivePath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("archive");
        startInfo.ArgumentList.Add("--format=zip");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add(baselineRef);

        try
        {
            using Process process = Process.Start(startInfo)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new BaselineException($"Could not create baseline archive for '{baselineRef}': {error.Trim()}");
            }
        }
        catch (BaselineException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BaselineException($"Could not create baseline archive for '{baselineRef}': {ex.Message}");
        }
    }
}

public sealed class BaselineException(string message) : Exception(message);
