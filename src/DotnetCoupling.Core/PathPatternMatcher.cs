namespace DotnetCoupling.Core;

internal static class PathPatternMatcher
{
    internal static bool IsMatch(string file, IReadOnlyList<string> patterns)
    {
        string normalizedFile = file.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        foreach (string pattern in patterns)
        {
            string normalizedPattern = pattern.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            if (MatchesPattern(normalizedFile, normalizedPattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPattern(string normalizedFile, string normalizedPattern)
    {
        if (normalizedPattern.StartsWith("**/", StringComparison.Ordinal))
        {
            string suffix = normalizedPattern[3..];
            if (suffix.StartsWith("*.", StringComparison.Ordinal))
            {
                return normalizedFile.EndsWith(suffix[1..], StringComparison.OrdinalIgnoreCase);
            }

            return normalizedFile.Contains("/" + suffix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || normalizedFile.EndsWith(suffix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            string[] parts = normalizedPattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            int index = 0;
            foreach (string part in parts)
            {
                int found = normalizedFile.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    return false;
                }

                index = found + part.Length;
            }

            return true;
        }

        return normalizedFile.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }
}
