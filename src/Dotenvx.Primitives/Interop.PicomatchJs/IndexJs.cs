using System.Text.RegularExpressions;

namespace Dotenvx.Interop.PicomatchJs;

/// <summary>
///     C# equivalent of Node.js 'picomatch' package (https://github.com/micromatch/picomatch)
/// </summary>
public static partial class IndexJs
{
    /// <remarks>
    /// `dot` mirrors real picomatch/`@dotenvx/primitives`' `match()`/`scan()` `dot` option, found
    /// missing entirely 2026-10-02 via a static audit cross-checking this reimplementation against
    /// the real, live-callable `match`/`scan` primitives exports. Confirmed live
    /// (`require('@dotenvx/primitives').scan(src, { ik: ['*'] })`) that a bare wildcard pattern does
    /// NOT match a key name starting with a literal `.` unless `dot:true` is passed OR the pattern
    /// itself already starts with a literal `.` (a pattern's own leading literal dot still matches a
    /// leading dot normally - the restriction is specifically on a WILDCARD character being allowed
    /// to consume a leading dot, not on literal dots in the pattern). This defaulted to `false` here
    /// (previously this function always behaved as `dot:true` unconditionally) because every KEY-
    /// filtering call site (`Decrypt`/`Encrypt`/`Rotate`/`GetAction`'s `-k`/`--include-key`/
    /// `--exclude-key`) needs the real default - confirmed live that `dotenvx get --include-key "*"`
    /// on a file containing a dot-prefixed key (e.g. `.SECRET=hidden`, itself a valid parseable key
    /// name) diverged: Node excluded it, this port included it. `Ls.cs`'s two call sites are the only
    /// ones needing `dot: true` explicitly, matching `ls.js`'s own real `{ dot: true }` options.
    /// Only guards position 0 of the whole input string, not the start of every `/`-separated path
    /// segment - sufficient for every current call site (key names are always single-segment; `Ls.cs`
    /// always passes `dot: true`, skipping the guard entirely) but would need a per-segment version
    /// if a future `dot: false` multi-segment path scenario shows up.
    /// </remarks>
    public static Func<string, bool> Picomatch(string[]? globs = null, string[]? ignore = null, bool dot = false)
    {
        // Include everything if globs is null and the ignore list is empty
        if (globs is null && ignore?.Length == 0)
        {
            return _ => true;
        }

        List<Func<string, bool>> includes = (globs ?? []).SelectMany(ExpandBraces).Select(g => Compile(g, dot)).ToList();
        List<Func<string, bool>> excludes = (ignore ?? []).SelectMany(ExpandBraces).Select(g => Compile(g, dot)).ToList();

        return s =>
        {
            return includes.Exists(include => include(s)) && !excludes.Exists(exclude => exclude(s));
        };
    }

    // A leading `!` (not an extglob `!(`) negates the rest of that glob, as in picomatch.
    private static Func<string, bool> Compile(string glob, bool dot)
    {
        if (glob.Length > 1 && glob[0] == '!' && glob[1] != '(')
        {
            Func<string, bool> inner = Compile(glob[1..], dot);
            return s => !inner(s);
        }

        Regex regex = ConvertToRegex(glob, dot);
        return regex.IsMatch;
    }

    // `{a,b}` brace alternation (only comma lists; a brace without a top-level comma stays literal).
    private static IEnumerable<string> ExpandBraces(string glob)
    {
        int open = glob.IndexOf('{');
        if (open < 0)
        {
            return [glob];
        }

        int depth = 0;
        int close = -1;
        List<int> commas = [];
        for (int i = open; i < glob.Length; i++)
        {
            if (glob[i] == '{')
            {
                depth++;
            }
            else if (glob[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    close = i;
                    break;
                }
            }
            else if (glob[i] == ',' && depth == 1)
            {
                commas.Add(i);
            }
        }

        if (close < 0 || commas.Count == 0)
        {
            return [glob];
        }

        string prefix = glob[..open];
        string suffix = glob[(close + 1)..];
        List<string> parts = [];
        int start = open + 1;
        foreach (int comma in commas)
        {
            parts.Add(glob[start..comma]);
            start = comma + 1;
        }

        parts.Add(glob[start..close]);
        return parts.SelectMany(p => ExpandBraces(prefix + p + suffix)).ToList();
    }

    private static Regex ConvertToRegex(string pattern, bool dot)
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

        // A wildcard never matches a leading dot unless dot:true or the pattern itself already
        // starts with a literal '.' - see this method's own doc comment.
        string leadingDotGuard = !dot && !pattern.StartsWith('.') ? @"(?!\.)" : "";

        // Lock to the whole line
        regexPattern = $"^{leadingDotGuard}{regexPattern}$";

        return new Regex(regexPattern, RegexOptions.Compiled);
    }

    [GeneratedRegex(@"\\\[(\^?[\w\d-]+)\]")]
    private static partial Regex CharacterSetRegex();
}