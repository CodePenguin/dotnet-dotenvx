using System.Text.RegularExpressions;

namespace Dotenvx.Interop.IgnoreJs;

public class IgnoreRule(string pattern, bool negation)
{
    private readonly Regex _regex = ConvertToRegex(pattern);

    public string Pattern { get; } = pattern;
    public bool Negation { get; } = negation;

    public bool Matches(string path)
    {
        return _regex.IsMatch(path);
    }

    private static Regex ConvertToRegex(string pattern)
    {
        // Handle directory-only patterns (ending with /)
        bool directoryOnly = pattern.EndsWith('/');
        if (directoryOnly)
        {
            pattern = pattern.TrimEnd('/');
        }

        // Escape special regex characters except * and ?
        string regexPattern = Regex.Escape(pattern)
            .Replace("/", @"\/") // Escape / since Regex.Escape does not
            .Replace(@"\*\*\/", "(?:.*/)?") // **/ matches any number of directories
            .Replace(@"\/\*\*", "(?:/.*)?") // /** matches anything after
            .Replace(@"\*\*", ".*") // ** matches anything
            .Replace(@"\*", @"[^\/]*") // * matches anything except /
            .Replace(@"\?", @"[^\/]"); // ? matches any single character except /

        // If pattern starts with /, it's anchored to root
        if (pattern.StartsWith('/'))
        {
            regexPattern = "^" + regexPattern[3..]; // Remove encoded /
        }
        else if (!pattern.Contains('/'))
        {
            // If no /, match anywhere in the path (basename matching)
            regexPattern = $@"(?:^|\/){regexPattern}";
        }
        else
        {
            // Otherwise anchor to start
            regexPattern = "^" + regexPattern;
        }

        // Anchor to end
        if (directoryOnly)
        {
            regexPattern += @"(?:\/|$)";
        }
        else
        {
            regexPattern += "$";
        }

        return new Regex(regexPattern, RegexOptions.Compiled);
    }
}