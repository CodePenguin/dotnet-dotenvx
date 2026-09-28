using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/replace.js</remarks>
public static partial class Helpers
{
    public static string Replace(string src, string key, string replaceValue)
    {
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

            // ([\s﻿]*)? not just (\s*)? so a byte-order-mark surviving at the very start of
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

    public static Dictionary<string, string> GetQuotes(string src)
    {
        Dictionary<string, string> obj = new();
        string lines = Regex.Replace(src, "\r\n?", "\n");
        // Leading [\s﻿]* (not just \s*) so a byte-order-mark surviving at the very start of
        // the decoded source doesn't stop the FIRST key from being recognized - see
        // Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md.
        MatchCollection matches = Regex.Matches(lines,
            @"(?:^|^)[\s﻿]*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)",
            RegexOptions.Multiline);

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