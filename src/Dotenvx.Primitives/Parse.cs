using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <remarks>Reference source: dotenvx/src/lib/helpers/dotenvParse.js</remarks>
public static partial class Primitives
{
    private static readonly Regex LineRegex = ParseRegex();

    public static Dictionary<string, object> Parse(string src, bool skipExpandForDoubleQuotes = false,
        bool skipConvertingWindowsNewlines = false, bool collectAllValues = false)
    {
        Dictionary<string, object> obj = new();

        // Convert buffer to string (assuming src is already string)
        string lines = src;

        // Convert line breaks to same format
        if (!skipConvertingWindowsNewlines)
        {
            lines = NormalizeNewLinesRegex().Replace(lines, "\n");
        }

        MatchCollection matches = LineRegex.Matches(lines);
        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;

            // Default undefined or null to empty string
            string value = match.Groups[2].Value;

            // Remove whitespace
            value = value.Trim();

            // Check if double quoted
            char maybeQuote = value.Length > 0 ? value[0] : '\0';

            // Remove surrounding quotes
            value = SurroundingQuotesRegex().Replace(value, "$2");

            // Unescape \' if single quoted (primitives' value pass; plain dotenv leaves it alone)
            if (maybeQuote == '\'')
            {
                value = value.Replace("\\'", "'");
            }

            // Expand newlines if double quoted
            if (maybeQuote == '"' && !skipExpandForDoubleQuotes)
            {
                value = value.Replace("\\n", "\n"); // newline
                value = value.Replace("\\r", "\r"); // carriage return
                value = value.Replace("\\t", "\t"); // tabs
            }

            if (collectAllValues)
            {
                // handle scenario where user mistakenly includes plaintext duplicate in .env
                obj.TryAdd(key, value);
            }
            else
            {
                // Add to object
                obj[key] = value;
            }
        }

        return obj;
    }

    // Leading [\s﻿]* (not just \s*) so a byte-order-mark surviving at the very start of the
    // decoded source (see Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md) doesn't stop the
    // FIRST key from being recognized - JS's \s regex metaclass matches U+FEFF; .NET's does not.
    // RegexOptions.ECMAScript makes \w ASCII-only (matching JS's \w exactly) instead of .NET's
    // default Unicode-aware \w - confirmed live that Node rejects a non-ASCII key entirely
    // (`HÉLLO=World` parses to `{}`, not a key named "HÉLLO") while .NET's default \w
    // silently accepted it as a valid key name.
    [GeneratedRegex(
        @"(?:^|^)[\s﻿]*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)",
        RegexOptions.Multiline | RegexOptions.ECMAScript)]
    private static partial Regex ParseRegex();

    [GeneratedRegex(@"\r\n?")]
    private static partial Regex NormalizeNewLinesRegex();

    [GeneratedRegex(@"^(['""`])([\s\S]*)\1$")]
    private static partial Regex SurroundingQuotesRegex();
}
