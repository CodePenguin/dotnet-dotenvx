using System.Text.RegularExpressions;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/append.js</remarks>
public static partial class Helpers
{
    public static string Append(string src, string key, string appendValue)
    {
        string output;
        string newPart = "";

        Dictionary<string, object> parsed = ScanToDictionary(src, true, true);
        Dictionary<string, string> quotes = GetQuotes(src);
        if (parsed.TryGetValue(key, out object? parsedValue))
        {
            string quote = quotes.GetValueOrDefault(key, "");
            string originalValue = (string)parsedValue;

            newPart += $"{key}={quote}{originalValue},{appendValue}{quote}";

            string escapedOriginalValue = EscapeForRegex(originalValue);

            // conditionally enforce end of line
            string enforceEndOfLine = "";
            if (string.IsNullOrEmpty(escapedOriginalValue))
            {
                enforceEndOfLine = "$"; // EMPTY scenario
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

            string saferInput = EscapeDollarSigns(newPart); // cleanse user inputted capture groups ($1, $2 etc)

            // $1 preserves spaces
            // $2 preserves export
            output = currentPart.Replace(src, "$1$2" + saferInput);
        }
        else
        {
            newPart += $"{key}=\"{appendValue}\"";

            // append
            if (src.EndsWith('\n'))
            {
                newPart += '\n';
            }
            else
            {
                newPart = '\n' + newPart;
            }

            output = src + newPart;
        }

        return output;
    }
}