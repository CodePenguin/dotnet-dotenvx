using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: @dotenvx/primitives' `sealed()` export (no readable source exists for this
/// package - see CLAUDE.md's live-oracle note). Decompiled via
/// `node -e "console.log(require('@dotenvx/primitives').sealed.toString())"` from within the
/// submodule dir, then confirmed behaviorally by calling it directly with concrete inputs
/// (duplicate keys, DOTENV_PUBLIC_KEY*/​*_PLAIN exemptions, empty content). Real semantics: parses
/// EVERY occurrence of every key (not just the last, unlike DotenvParse's default last-value-wins,
/// and unlike DotenvParse's own `collectAllValues` param, which despite its name only keeps the
/// FIRST occurrence via TryAdd - neither is suitable here), and requires every one of those values
/// to be encrypted, except keys matching StartsWith("DOTENV_PUBLIC_KEY") or EndsWith("_PLAIN"),
/// which are exempt regardless of their value. Empty content is vacuously sealed (true).
/// </remarks>
public static partial class Helpers
{
    public static bool Sealed(string content)
    {
        MatchCollection matches = DotenvParseRegex().Matches(content);
        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            if (key.StartsWith("DOTENV_PUBLIC_KEY") || key.EndsWith("_PLAIN"))
            {
                continue;
            }

            string value = SurroundingQuotesRegex().Replace(match.Groups[2].Value.Trim(), "$2");
            if (!IsEncrypted(value))
            {
                return false;
            }
        }

        return true;
    }
}
