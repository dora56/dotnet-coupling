using System.Text;
using System.Text.RegularExpressions;

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
        if (!ContainsWildcard(normalizedPattern))
        {
            return normalizedFile.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        string regex = CreateGlobRegex(normalizedPattern);
        return Regex.IsMatch(normalizedFile, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsWildcard(string normalizedPattern)
    {
        return normalizedPattern.Contains('*', StringComparison.Ordinal)
            || normalizedPattern.Contains('?', StringComparison.Ordinal);
    }

    private static string CreateGlobRegex(string normalizedPattern)
    {
        StringBuilder builder = new();
        builder.Append('^');
        if (!normalizedPattern.StartsWith('/')
            && !normalizedPattern.StartsWith("**/", StringComparison.Ordinal)
            && normalizedPattern.Contains('/', StringComparison.Ordinal))
        {
            builder.Append("(?:.*/)?");
        }

        for (int index = 0; index < normalizedPattern.Length; index++)
        {
            char current = normalizedPattern[index];
            if (current == '*')
            {
                bool isRecursive = index + 1 < normalizedPattern.Length && normalizedPattern[index + 1] == '*';
                if (isRecursive)
                {
                    bool consumesSlash = index + 2 < normalizedPattern.Length && normalizedPattern[index + 2] == '/';
                    builder.Append(consumesSlash ? "(?:.*/)?" : ".*");
                    index += consumesSlash ? 2 : 1;
                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            builder.Append(current == '?' ? "[^/]" : Regex.Escape(current.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }
}
