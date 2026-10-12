using System.Globalization;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// JavaScript's `toUpperCase` / `toLowerCase` (full Unicode case mapping, locale independent): "straße" becomes "STRASSE", "ǆ"
/// becomes "Ǆ", "İ" lowercases to "i̇", and a capital sigma at the end of a word lowercases to "ς". .NET's invariant casing is a
/// one-to-one mapping and gives different results, so the names derived from user data (`.env.<name>` to `DOTENV_PRIVATE_KEY_<NAME>`)
/// use this instead. The tables are generated from Node by scripts/gen-js-case-tables.js.
/// </remarks>
internal static partial class JsCase
{
    private static readonly Lazy<Dictionary<int, string>> Upper = new(() => Parse(UpperData!));
    private static readonly Lazy<Dictionary<int, string>> Lower = new(() => Parse(LowerData!));

    public static string ToUpper(string value)
    {
        if (value.All(char.IsAscii))
        {
            return value.ToUpperInvariant();
        }

        return Map(value, Upper.Value);
    }

    public static string ToLower(string value)
    {
        if (value.All(char.IsAscii))
        {
            return value.ToLowerInvariant();
        }

        return Map(value, Lower.Value, finalSigma: true);
    }

    private static string Map(string value, Dictionary<int, string> table, bool finalSigma = false)
    {
        var result = new StringBuilder(value.Length);
        var runes = value.EnumerateRunes().ToArray();
        for (int i = 0; i < runes.Length; i++)
        {
            int cp = runes[i].Value;
            if (finalSigma && cp == 0x3A3)
            {
                result.Append(IsFinalSigma(runes, i) ? 'ς' : 'σ');
            }
            else if (table.TryGetValue(cp, out string? mapped))
            {
                result.Append(mapped);
            }
            else
            {
                result.Append(runes[i].ToString());
            }
        }

        return result.ToString();
    }

    // Unicode Final_Sigma: preceded by a cased letter (ignoring case-ignorable characters) and not followed by one
    private static bool IsFinalSigma(Rune[] runes, int index)
    {
        int before = index - 1;
        while (before >= 0 && IsCaseIgnorable(runes[before]))
        {
            before--;
        }

        int after = index + 1;
        while (after < runes.Length && IsCaseIgnorable(runes[after]))
        {
            after++;
        }

        return before >= 0 && IsCased(runes[before]) && !(after < runes.Length && IsCased(runes[after]));
    }

    private static bool IsCased(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);

        return category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter;
    }

    private static bool IsCaseIgnorable(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);

        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol ||
               rune.Value is '\'' or '.' or ':' or 0xB7 or 0x2018 or 0x2019 or 0x2024 or 0x2027 or 0xAD;
    }

    private static Dictionary<int, string> Parse(string[] chunks)
    {
        var table = new Dictionary<int, string>();
        foreach (string entry in chunks.SelectMany(chunk => chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            string[] parts = entry.Split(':');
            table[Convert.ToInt32(parts[0], 16)] = string.Concat(parts[1].Split(',').Select(hex => char.ConvertFromUtf32(Convert.ToInt32(hex, 16))));
        }

        return table;
    }
}
