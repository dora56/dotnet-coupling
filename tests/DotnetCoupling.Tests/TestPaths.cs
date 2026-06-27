namespace DotnetCoupling.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dotnet-coupling.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        }
    }

    public static string Fixture(string name)
    {
        return Path.Combine(RepositoryRoot, "tests", "fixtures", name);
    }

    public static string Golden(string name)
    {
        return Path.Combine(RepositoryRoot, "tests", "golden", name);
    }

    public static string NormalizeFixturePath(string text, string fixturePath)
    {
        return text.Replace(fixturePath, "<FIXTURE>", StringComparison.Ordinal);
    }
}
