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

        List<Regex> includes = (globs ?? []).Select(g => ConvertToRegex(g, dot)).ToList();
        List<Regex> excludes = (ignore ?? []).Select(g => ConvertToRegex(g, dot)).ToList();

        return s =>
        {
            return includes.Exists(include => include.IsMatch(s)) && !excludes.Exists(exclude => exclude.IsMatch(s));
        };
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