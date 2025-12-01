using System.Text.RegularExpressions;

namespace Dotenvx.Interop.PicomatchJs;

/// <summary>
///     C# equivalent of Node.js 'picomatch' package (https://github.com/micromatch/picomatch)
/// </summary>
public static partial class IndexJs
{
    public static Func<string, bool> Picomatch(string[]? globs = null, string[]? ignore = null)
    {
        // Include everything if globs is null and the ignore list is empty
        if (globs is null && ignore?.Length == 0)
        {
            return _ => true;
        }

        List<Regex> includes = (globs ?? []).Select(ConvertToRegex).ToList();
        List<Regex> excludes = (ignore ?? []).Select(ConvertToRegex).ToList();

        return s =>
        {
            return includes.Exists(include => include.IsMatch(s)) && !excludes.Exists(exclude => exclude.IsMatch(s));
        };
    }

    private static Regex ConvertToRegex(string pattern)
    {
        // Escape special regex characters except * and ?
        string regexPattern = Regex.Escape(pattern)
            .Replace("/", @"\/") // Escape / since Regex.Escape does not
            .Replace(@"\*\*\/", @"(?:.*\/)?") // **/ matches any number of directories
            .Replace(@"\/\*\*", @"(?:\/.*)?") // /** matches anything after
            .Replace(@"\*\*", ".*") // ** matches anything
            .Replace(@"\*", @"[^\/]*") // * matches anything except /
            .Replace(@"\?", @"[^\/]"); // ? matches any single character except /

        // [a-z] matches a single character from a range of characters
        regexPattern = CharacterSetRegex().Replace(regexPattern, "[$1]");

        // Lock to the whole line
        regexPattern = $"^{regexPattern}$";

        return new Regex(regexPattern, RegexOptions.Compiled);
    }

    [GeneratedRegex(@"\\\[(\^?[\w\d-]+)\]")]
    private static partial Regex CharacterSetRegex();
}