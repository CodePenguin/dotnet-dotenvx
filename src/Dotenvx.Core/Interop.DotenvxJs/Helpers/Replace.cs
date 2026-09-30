using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/replace.js</remarks>
public static partial class Helpers
{
    public static string Replace(string src, string key, string replaceValue)
    {
        // @dotenvx/primitives' upsert() (the live-verified successor to this file, since
        // replace.js no longer exists at HEAD) replaces EVERY occurrence of a duplicate key with
        // the new value, each keeping its own original quote style - confirmed live against Node
        // (`FOO=old1` / `FOO=old2` both become the new value, `FOO="old1"` keeps its double
        // quotes while a neighboring unquoted `FOO=old2` stays unquoted). The single-occurrence
        // path below predates that finding and only ever matched the LAST occurrence's value
        // text, silently leaving earlier duplicates stale - fork off to the multi-occurrence path
        // first so that well-tested single/no-occurrence behavior is untouched.
        List<(int ValueIndex, int ValueLength, string Value, string Quote)> occurrences =
            FindKeyOccurrences(src, key);
        if (occurrences.Count > 1)
        {
            return ReplaceAllOccurrences(src, occurrences, replaceValue);
        }

        Dictionary<string, object> parsed = DotenvParse(src, true, true);
        Dictionary<string, string> quotes = GetQuotes(src);

        if (parsed.TryGetValue(key, out object? value))
        {
            string quote = quotes.GetValueOrDefault(key, "");
            string newPart = $"{key}={quote}{replaceValue}{quote}";

            string originalValue = (string)value;
            string escapedOriginalValue = EscapeForRegex(originalValue);

            string enforceEndOfLine = "";
            if (string.IsNullOrEmpty(escapedOriginalValue))
            {
                enforceEndOfLine = "$";
                // handle empty quote and consecutive newlines
                Match newlineMatch = Regex.Match(src, $"{Regex.Escape(key)}\\s*=\\s*\n\n", RegexOptions.Multiline);
                if (quote == "" && newlineMatch.Success)
                {
                    int newlineCount = newlineMatch.Value.Count(c => c == '\n') - 1;
                    for (int i = 0; i < newlineCount; i++)
                    {
                        newPart += "\n";
                    }
                }
            }

            // ([\s\uFEFF]*)? not just (\s*)? so a byte-order-mark surviving at the very start of
            // the decoded source is captured and preserved (not lost) when this key is the FIRST
            // one in the file - see Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md.
            Regex currentPart = new(
                "^" + // start of line
                "([\\s\\uFEFF]*)?" + // spaces
                "(export\\s+)?" + // export
                Regex.Escape(key) + // KEY
                "\\s*=\\s*" + // spaces (KEY = value)
                "[\"'`]?" + // open quote
                escapedOriginalValue + // escaped value
                "[\"'`]?" + // close quote
                enforceEndOfLine,
                RegexOptions.Multiline
            );

            string saferInput = EscapeDollarSigns(newPart);

            return currentPart.Replace(src, "$1$2" + saferInput);
        }
        else
        {
            if (src.Trim().Length == 0)
            {
                // matches Node's upsert on empty src: `KEY="value"\n` (no leading newline)
                return $"{key}=\"{replaceValue}\"\n";
            }

            string newPart = $"{key}=\"{replaceValue}\"";

            if (src.EndsWith("\n"))
            {
                newPart += "\n";
            }
            else
            {
                newPart = "\n" + newPart;
            }

            return src + newPart;
        }
    }

    // Finds every occurrence of `key` in `src` (not just the first/last), returning each
    // occurrence's value span (for position-based splicing, so an untouched replacement of one
    // occurrence never shifts another's index) plus its already-quote-stripped value and original
    // quote character - same per-match logic as GetQuotes, generalized to collect all matches for
    // a specific key in source order instead of collapsing them into one dictionary entry.
    private static List<(int ValueIndex, int ValueLength, string Value, string Quote)> FindKeyOccurrences(
        string src, string key)
    {
        List<(int, int, string, string)> result = [];
        foreach (Match match in DotenvParseRegex().Matches(src))
        {
            if (match.Groups[1].Value != key)
            {
                continue;
            }

            Group valueGroup = match.Groups[2];
            string rawValue = valueGroup.Value.Trim();
            string maybeQuote = rawValue.Length > 0 ? rawValue[0].ToString() : "";
            string stripped = Regex.Replace(rawValue, @"^(['""`])([\s\S]*)\1$", "$2");
            string quote = maybeQuote == stripped.FirstOrDefault().ToString() ? "" : maybeQuote;
            result.Add((valueGroup.Index, valueGroup.Length, stripped, quote));
        }

        return result;
    }

    // Splices each occurrence's own new value in, preserving that occurrence's own quote style,
    // working backward so earlier occurrences' indices stay valid while later ones are replaced.
    // Plain string splicing (not Regex.Replace) needs no $-sign escaping of replaceValue.
    private static string ReplaceAllOccurrences(
        string src, IReadOnlyList<(int ValueIndex, int ValueLength, string Value, string Quote)> occurrences,
        string replaceValue)
    {
        string result = src;
        for (int i = occurrences.Count - 1; i >= 0; i--)
        {
            (int index, int length, _, string quote) = occurrences[i];
            result = result[..index] + quote + replaceValue + quote + result[(index + length)..];
        }

        return result;
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