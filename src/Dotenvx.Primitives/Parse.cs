using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <summary>One key/value pair found by <see cref="Primitives.Scan" />; Quote is '\0' when unquoted.</summary>
public readonly record struct ScanEntry(string Key, string Value, char Quote, string? Comment = null);

/// <remarks>
/// Counterpart of the @dotenvx/primitives `scan`/`parse` exports (no readable source; the minified
/// function bodies were read via `Function.prototype.toString()` and the result is checked against
/// the live package by PrimitivesOracleParityTest). `Scan` is the one shared line-parser: this
/// project's `Parse`/`Sealed`/`PublicKeys` and Dotenvx.Core's `Parse` engine all build on it.
/// </remarks>
public static partial class Primitives
{
    // JS `\s` (ECMA-262 WhiteSpace + LineTerminator) - .NET's `\s` differs (no U+FEFF, extra U+0085)
    // and RegexOptions.ECMAScript narrows it to ASCII, so spell the class out.
    private static readonly char[] JsWhitespaceChars =
    [
        (char)9, (char)10, (char)11, (char)12, (char)13, (char)32, (char)0xA0, (char)0x1680,
        (char)0x2000, (char)0x2001, (char)0x2002, (char)0x2003, (char)0x2004, (char)0x2005,
        (char)0x2006, (char)0x2007, (char)0x2008, (char)0x2009, (char)0x200A, (char)0x2028,
        (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000, (char)0xFEFF
    ];

    internal static readonly string Ws = new(JsWhitespaceChars);

    // JS line terminators, used for `^`/`$`/`.` under the `m` flag (.NET's Multiline only knows \n).
    private static readonly string Terminators = new([(char)10, (char)13, (char)0x2028, (char)0x2029]);

    internal static readonly string LineStart = "(?<![^" + Terminators + "])";
    internal static readonly string LineEnd = "(?![^" + Terminators + "])";

    private static readonly Regex ScanRegex = new(
        LineStart + "[" + Ws + "]*(?:export[" + Ws + "]+)?([A-Za-z0-9_.-]+)" +
        "(?:[" + Ws + "]*=[" + Ws + "]*?|:[" + Ws + "]+?)" +
        "([" + Ws + "]*'(?:\\\\'|[^'])*'|[" + Ws + "]*\"(?:\\\\\"|[^\"])*\"|[" + Ws + "]*`(?:\\\\`|[^`])*`|[^#\\r\\n]+)?" +
        "[" + Ws + "]*(?:#([^" + Terminators + "]*))?" + LineEnd,
        RegexOptions.CultureInvariant);

    private static readonly Regex SurroundingQuotes = new("^(['\"`])([\\s\\S]*)\\1$", RegexOptions.CultureInvariant);

    internal static string JsTrimStart(string s)
    {
        return s.TrimStart(JsWhitespaceChars);
    }

    public static string JsTrim(string s)
    {
        return s.Trim(JsWhitespaceChars);
    }

    public static List<ScanEntry> Scan(string? src, bool expandDoubleQuotedNewlines = true,
        bool convertWindowsNewlines = true)
    {
        string lines = src ?? "";
        if (convertWindowsNewlines)
        {
            lines = NormalizeNewLinesRegex().Replace(lines, "\n");
        }

        List<ScanEntry> entries = [];
        foreach (Match match in ScanRegex.Matches(lines))
        {
            string raw = JsTrim(match.Groups[2].Value);
            char quote = raw.Length > 0 && raw[0] is '\'' or '"' or '`' ? raw[0] : '\0';
            string value = SurroundingQuotes.Replace(raw, "$2");

            if (quote == '\'')
            {
                value = value.Replace("\\'", "'");
            }

            if (quote == '"' && expandDoubleQuotedNewlines)
            {
                value = value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
            }

            entries.Add(new ScanEntry(match.Groups[1].Value, value, quote,
                match.Groups[3].Success ? JsTrim(match.Groups[3].Value) : null));
        }

        return entries;
    }

    [GeneratedRegex(@"\r\n?")]
    private static partial Regex NormalizeNewLinesRegex();
}
