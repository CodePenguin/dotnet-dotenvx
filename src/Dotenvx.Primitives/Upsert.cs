using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `upsert` export - a faithful port of the real function
/// bodies (read via the minified dist), checked against the live package by
/// PrimitivesOracleParityTest. Every occurrence of a duplicate key is replaced (each keeps its own
/// quote style) through a unique placeholder, then the new value is substituted literally.
/// </remarks>
public static partial class Primitives
{
    public static string Upsert(string src, string key, string replaceValue)
    {
        return UpsertCore(src, key, null, replaceValue);
    }

    // an array applies one replacement per existing occurrence; a new key gets the array joined with commas (JS template coercion)
    public static string Upsert(string src, string key, string[] replaceValues)
    {
        return UpsertCore(src, key, replaceValues, string.Join(",", replaceValues));
    }

    private static string UpsertCore(string src, string key, string[]? replaceValues, string replaceValue)
    {
        List<string> values = [];
        foreach (ScanEntry entry in Scan(src, false, false))
        {
            if (entry.Key == key)
            {
                values.Add(entry.Value);
            }
        }

        if (values.Count == 0)
        {
            string line = $"{key}=\"{replaceValue}\"";
            line = src.Length == 0 || src.EndsWith('\n') ? line + "\n" : "\n" + line;
            return src + line;
        }

        string current = src;
        for (int i = 0; i < values.Count; i++)
        {
            current = ReplaceFirstOccurrence(current, key, values[i], $"\0DOTENVX_UPSERT_{i}\0");
        }

        for (int i = 0; i < (replaceValues?.Length ?? values.Count); i++)
        {
            current = current.Replace($"\0DOTENVX_UPSERT_{i}\0", replaceValues?[i] ?? replaceValue);
        }

        return current;
    }

    private static string ReplaceFirstOccurrence(string src, string key, string value, string placeholder)
    {
        string escapedKey = EscapeForRegex(key);
        string escapedValue = EscapeForRegex(value);
        string ws = "[" + Ws + "]";
        Regex pattern = new(
            LineStart + "(" + ws + "*)?(export" + ws + "+)?" + escapedKey + ws + "*=" + ws + "*([\"'`]?)" +
            escapedValue + "\\3" + (escapedValue.Length == 0 ? LineEnd : ""),
            RegexOptions.CultureInvariant);

        Match match = pattern.Match(src);
        if (!match.Success)
        {
            return src;
        }

        string leading = match.Groups[1].Value;
        string export = match.Groups[2].Value;
        string quote = match.Groups[3].Value;
        string replacement = $"{key}={quote}{placeholder}{quote}";

        // handle empty quote and consecutive newlines
        Match newlines = Regex.Match(src, escapedKey + ws + "*=" + ws + "*\n\n", RegexOptions.CultureInvariant);
        if (escapedValue.Length == 0 && quote.Length == 0 && newlines.Success)
        {
            int extra = newlines.Value.Count(c => c == '\n') - 1;
            replacement += new string('\n', extra);
        }

        return src[..match.Index] + leading + export + replacement + src[(match.Index + match.Length)..];
    }

    public static Dictionary<string, string> GetQuotes(string src)
    {
        Dictionary<string, string> obj = new();
        string lines = Regex.Replace(src, "\r\n?", "\n");
        // Leading [\s\uFEFF]* (not just \s*) so a byte-order-mark surviving at the very start of
        // the decoded source doesn't stop the FIRST key from being recognized, and
        // RegexOptions.ECMAScript makes \w ASCII-only (matching JS's \w exactly) instead of .NET's
        // default Unicode-aware \w - see Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md.
        MatchCollection matches = Regex.Matches(lines,
            @"(?:^|^)[\s\uFEFF]*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)",
            RegexOptions.Multiline | RegexOptions.ECMAScript);

        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();
            string maybeQuote = value.Length > 0 ? value[0].ToString() : "";
            value = Regex.Replace(value, @"^(['""`])([\s\S]*)\1$", "$2");

            if (maybeQuote == value.FirstOrDefault().ToString())
            {
                obj[key] = "";
            }
            else
            {
                obj[key] = maybeQuote;
            }
        }

        return obj;
    }

    public static string EscapeDollarSigns(string value)
    {
        return value.Replace("$", "$$");
    }
}
