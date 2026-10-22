using System.Text.RegularExpressions;

namespace Dotenvx.Interop.IgnoreJs;

internal partial class IgnoreRule(string pattern, bool negation)
{
    private readonly Regex _regex = ConvertToRegex(pattern);

    public string Pattern { get; } = pattern;
    public bool Negation { get; } = negation;

    public bool Matches(string path)
    {
        return _regex.IsMatch(path);
    }

    /// <remarks>
    /// Rewritten 2026-10-02 after a static audit cross-checked this reimplementation against the
    /// real `ignore` npm package (live-callable via `@dotenvx/tooling`'s `ignore` export, the same
    /// one `prebuild.js`/`precommit.js` actually use) - the original conversion got several gitignore
    /// semantics wrong, confirmed live via `ignore().add(pattern).ignores(path)`:
    /// - A pattern with NO `/` (or only a single TRAILING `/`) matches at any depth as a whole PATH
    ///   SEGMENT, not just a prefix/basename string - `node_modules` must match `a/node_modules/b`,
    ///   not just a bare `node_modules` path. The old code's `(?:^|\/){pattern}` had no trailing
    ///   boundary at all, so `node_modules` incorrectly failed to match `node_modules/foo` (missing
    ///   "everything nested under a match is also ignored" semantics) while also incorrectly
    ///   MATCHING prefix-glued names it shouldn't have in other cases.
    /// - A directory-only pattern (single trailing `/`, no other `/`) does NOT match the bare name
    ///   itself - only paths that are actually nested inside it (`build/` does not match `build`,
    ///   only `build/file`) - the old code's `(?:\/|$)` suffix incorrectly matched the bare name too.
    /// - A pattern containing `/` anywhere except as the single trailing character is ANCHORED to
    ///   the root (`/build`, `foo/bar`, `foo/**`) - but, like the basename case, matching the exact
    ///   path should ALSO match anything nested under it (`/build` matches `build/file` too) - the
    ///   old code's anchored branch had no such suffix at all, so `/build` wrongly failed to match
    ///   `build/file`.
    /// - `/**` requires a LITERAL, mandatory slash before the wildcard (`foo/**` must NOT match the
    ///   bare `foo`) - the old code's `(?:/.*)?` made that slash optional, incorrectly matching `foo`
    ///   itself. This is a different, stricter rule from `Interop.PicomatchJs`'s `/**` handling
    ///   (which legitimately wants the slash optional there) - these are two separate regex-building
    ///   functions for two different matching specs (gitignore vs. picomatch), not one shared bug.
    /// </remarks>
    private static Regex ConvertToRegex(string pattern)
    {
        bool anchored = pattern.StartsWith('/') ||
                         (pattern.EndsWith('/') ? pattern[..^1] : pattern).Contains('/');

        string body = pattern.StartsWith('/') ? pattern[1..] : pattern;

        bool directoryOnly = body.EndsWith('/');
        if (directoryOnly)
        {
            body = body[..^1];
        }

        // Escape special regex characters except * and ?
        string regexBody = Regex.Escape(body)
            .Replace("/", @"\/") // Escape / since Regex.Escape does not
            .Replace(@"\*\*\/", "(?:.*/)?") // **/ matches any number of directories
            .Replace(@"\/\*\*", @"\/.*") // /** requires a literal slash, then matches anything after
            .Replace(@"\*\*", ".*") // ** matches anything
            .Replace(@"\*", @"[^\/]*") // * matches anything except /
            .Replace(@"\?", @"[^\/]"); // ? matches any single character except /

        regexBody = CharacterSetRegex().Replace(regexBody, "[$1]");

        string prefix = anchored ? "^" : @"(?:^|.*\/)";
        // Matching a directory also matches everything nested under it - except a directory-only
        // (trailing-slash) pattern, which requires that literal nested slash rather than also
        // matching the bare name.
        string suffix = directoryOnly ? @"\/.*$" : @"(?:\/.*)?$";

        return new Regex(prefix + regexBody + suffix, RegexOptions.Compiled);
    }

    // [a-z] matches a single character from a range of characters - confirmed live
    // (ignore().add('file[0-9].txt')) that the real package supports this too.
    [GeneratedRegex(@"\\\[(\^?[\w\d-]+)\]")]
    private static partial Regex CharacterSetRegex();
}
