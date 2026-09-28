using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/validateEnvExample.js</remarks>
public static partial class Helpers
{
    public static DotenvxException? ValidateEnvExample(IReadOnlyDictionary<string, string> env, string filepath = ".env.example")
    {
        if (!Fsx.ExistsSync(filepath))
        {
            return new MissingEnvExampleException();
        }

        string exampleSrc = Fsx.ReadFileX(filepath);
        (Dictionary<string, string[]> example, Dictionary<string, string?[]> comments) = ScanKeysAndComments(exampleSrc);

        if (!ValidateExample(example, env, comments, out string[] missingRequired))
        {
            return new ValidationFailedException($"missing required ({string.Join(", ", missingRequired)})");
        }

        return null;
    }

    /// <remarks>
    /// A narrow, `.env.example`-specific stand-in for @dotenvx/primitives' `scan()` (no readable
    /// source exists for that package - see CLAUDE.md's live-oracle note). Live-probed scan()'s
    /// real output shape (`node -e "console.log(JSON.stringify(require('@dotenvx/primitives')
    /// .scan(src)))"`, 2026-09-24): `parsed: {key: [values...]}` (every occurrence, not just the
    /// last) and `comments: {key: [commentOrNull...]}` (parallel array, one entry per occurrence).
    /// Only the KEY SET and per-key comments matter for validate.js's algorithm - .env.example's
    /// own values are always blanked placeholders genexample.js writes, never consulted - so this
    /// doesn't attempt a fully general multiline/escaped-quote scan() reimplementation, just enough
    /// to extract keys and trailing `# comment` text per line, matching genexample.js's own
    /// single-line-per-key output format.
    /// </remarks>
    private static (Dictionary<string, string[]>, Dictionary<string, string?[]>) ScanKeysAndComments(string src)
    {
        Dictionary<string, List<string>> parsed = [];
        Dictionary<string, List<string?>> comments = [];

        foreach (Match match in ScanLineRegex().Matches(src))
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            string? comment = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

            if (!parsed.TryGetValue(key, out List<string>? values))
            {
                values = [];
                parsed[key] = values;
                comments[key] = [];
            }

            values.Add(value);
            comments[key].Add(comment);
        }

        return (parsed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
            comments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()));
    }

    // Leading [\s﻿]* (not just \s*) so a byte-order-mark surviving at the very start of the
    // decoded source (see Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md) doesn't stop the
    // FIRST key from being recognized - JS's \s regex metaclass matches U+FEFF; .NET's does not.
    [GeneratedRegex(
        @"^[\s﻿]*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#(.*))?$",
        RegexOptions.Multiline)]
    private static partial Regex ScanLineRegex();
}
