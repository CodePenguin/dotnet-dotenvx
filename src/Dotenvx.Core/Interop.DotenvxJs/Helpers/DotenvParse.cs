using System.Text.RegularExpressions;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static readonly Regex LineRegex = DotenvParseRegex();

    public static Dictionary<string, object> DotenvParse(string src, bool skipExpandForDoubleQuotes = false,
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
            value = Regex.Replace(value, @"^(['""`])([\s\S]*)\1$", "$2");

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
                if (!obj.ContainsKey(key))
                {
                    obj[key] = new List<string>();
                }

                ((List<string>)obj[key]).Add(value);
            }
            else
            {
                // Add to object
                obj[key] = value;
            }
        }

        return obj;
    }

    [GeneratedRegex(
        @"(?:^|^)\s*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)",
        RegexOptions.Multiline)]
    private static partial Regex DotenvParseRegex();

    [GeneratedRegex(@"\r\n?")]
    private static partial Regex NormalizeNewLinesRegex();
}