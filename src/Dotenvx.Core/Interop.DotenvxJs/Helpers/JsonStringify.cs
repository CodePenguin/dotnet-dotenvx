using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Not a port of any file - reproduces JS's `JSON.stringify` string escaping, which only escapes `"`, `\`,
/// control characters below U+0020 (lowercase `\u001f`) and lone surrogates. System.Text.Json (even with
/// UnsafeRelaxedJsonEscaping) still emits uppercase `\uXXXX` for DEL, U+2028/2029, U+FEFF, U+00A0, every
/// supplementary-plane character, etc., and escapes `&amp;&lt;&gt;+'` by default - so a `DATABASE_URL`
/// containing `&amp;` printed by `get` differed from Node's bytes. Indented output uses "\n", not
/// Environment.NewLine.
/// </remarks>
public static partial class Helpers
{
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true, NewLine = "\n"
    };

    public static string JsonStringify(object? value, bool indented = false)
    {
        string json = JsonSerializer.Serialize(value, indented ? IndentedJson : CompactJson);
        return JsonEscapeRegex().Replace(json, UnescapeLikeJavaScript);
    }

    private static string UnescapeLikeJavaScript(Match match)
    {
        if (match.Groups[1].Success)
        {
            return char.ConvertFromUtf32(char.ConvertToUtf32(
                (char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber),
                (char)int.Parse(match.Groups[2].Value, NumberStyles.HexNumber)));
        }

        if (!match.Groups[3].Success)
        {
            return match.Value;
        }

        int codeUnit = int.Parse(match.Groups[3].Value, NumberStyles.HexNumber);
        return codeUnit is < 0x20 or >= 0xD800 and <= 0xDFFF
            ? @"\u" + codeUnit.ToString("x4", CultureInfo.InvariantCulture)
            : ((char)codeUnit).ToString();
    }

    // Walks escapes left to right so an escaped backslash (`\\`) followed by `u00e9` text is never mistaken for
    // a unicode escape; a valid surrogate pair is matched as one unit and collapsed to the real character.
    [GeneratedRegex(@"\\(?:u(D[89AB][0-9A-F]{2})\\u(D[C-F][0-9A-F]{2})|u([0-9A-F]{4})|.)", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex JsonEscapeRegex();
}
