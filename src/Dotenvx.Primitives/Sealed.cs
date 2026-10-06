
namespace Dotenvx.Primitives;

/// <remarks>
/// Reference source: @dotenvx/primitives' `sealed()` export (no readable source exists for this
/// package - see docs/porting-traps.md, live-oracle note). Decompiled via
/// `node -e "console.log(require('@dotenvx/primitives').sealed.toString())"` from within the
/// submodule dir, then confirmed behaviorally by calling it directly with concrete inputs
/// (duplicate keys, DOTENV_PUBLIC_KEY*/​*_PLAIN exemptions, empty content). Real semantics: parses
/// EVERY occurrence of every key (not just the last, unlike Parse's default last-value-wins, and
/// unlike Parse's own `collectAllValues` param, which despite its name only keeps the FIRST
/// occurrence via TryAdd - neither is suitable here), and requires every one of those values to be
/// encrypted, except keys matching StartsWith("DOTENV_PUBLIC_KEY") or EndsWith("_PLAIN"), which are
/// exempt regardless of their value. Empty content is vacuously sealed (true).
/// </remarks>
public static partial class Primitives
{
    public static bool Sealed(string content)
    {
        foreach (ScanEntry entry in Scan(content))
        {
            if (entry.Key.StartsWith("DOTENV_PUBLIC_KEY") || entry.Key.EndsWith("_PLAIN"))
            {
                continue;
            }

            if (!Encrypted(entry.Value))
            {
                return false;
            }
        }

        return true;
    }
}
