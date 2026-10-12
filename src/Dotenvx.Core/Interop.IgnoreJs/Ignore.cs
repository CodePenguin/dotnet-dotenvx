namespace Dotenvx.Interop.IgnoreJs;

/// <summary>
///     C# equivalent of Node.js 'ignore' package (https://github.com/kaelzhang/node-ignore)
/// </summary>
public class Ignore
{
    private readonly List<IgnoreRule> _rules = [];

    /// <summary>
    ///     Adds ignore patterns from a string (like .gitignore content)
    /// </summary>
    public Ignore Add(string patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
        {
            return this;
        }

        string[] lines = patterns.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            // Check if it's a negation pattern
            bool negation = trimmed.StartsWith('!');
            if (negation)
            {
                trimmed = trimmed[1..];
            }

            _rules.Add(new IgnoreRule(trimmed, negation));
        }

        return this;
    }

    /// <summary>
    ///     Adds ignore patterns from an array/list
    /// </summary>
    public Ignore Add(IEnumerable<string> patterns)
    {
        foreach (string pattern in patterns)
        {
            Add(pattern);
        }

        return this;
    }

    private static readonly System.Text.RegularExpressions.Regex InvalidPath = new(@"^\.{0,2}/|^\.?\.$");
    private static readonly System.Text.RegularExpressions.Regex DriveLetter = new(@"^[A-Za-z]:/");


    /// <summary>
    ///     Tests if a path should be ignored
    /// </summary>
    public bool Ignores(string path)
    {
        // node-ignore's checkPath: the path must be a non-empty, relative path (backslashes are separators on Windows only)
        string original = path;
        if (path is null)
        {
            throw new InvalidOperationException("path must be a string, but got `null`");
        }

        if (path.Length == 0)
        {
            throw new InvalidOperationException("path must not be empty");
        }

        if (OperatingSystem.IsWindows())
        {
            path = path.Replace('\\', '/');
        }

        if (InvalidPath.IsMatch(path) || (OperatingSystem.IsWindows() && DriveLetter.IsMatch(path)))
        {
            throw new InvalidOperationException(
                $"path should be a `path.relative()`d string, but got \"{original}\"");
        }

        bool ignored = false;

        foreach (IgnoreRule rule in _rules)
        {
            if (rule.Matches(path))
            {
                ignored = !rule.Negation;
            }
        }

        return ignored;
    }

    /// <summary>
    ///     Filters a list of paths, returning only those that are NOT ignored
    /// </summary>
    public IEnumerable<string> Filter(IEnumerable<string> paths)
    {
        return paths.Where(path => !Ignores(path));
    }

    /// <summary>
    ///     Creates an Ignore instance from a .gitignore file
    /// </summary>
    public static Ignore FromFile(string filePath)
    {
        Ignore ignore = new();

        if (!File.Exists(filePath))
        {
            return ignore;
        }

        string content = File.ReadAllText(filePath);
        ignore.Add(content);

        return ignore;
    }
}