using Dotenvx.Primitives;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Not a primitives export: dotenvx-side glue that turns `Primitives.Scan`'s ordered entries into a key to
/// value dictionary (last value wins, or first value wins with `collectAllValues`), the shape most Core
/// callers (append/replace/key lookup/encrypt/decrypt/sets) want. The parsing itself lives in `Scan`.
/// </remarks>
internal static partial class Helpers
{
    public static Dictionary<string, object> ScanToDictionary(string? src, bool skipExpandForDoubleQuotes = false,
        bool skipConvertingWindowsNewlines = false, bool collectAllValues = false)
    {
        Dictionary<string, object> obj = [];
        foreach (ScanEntry entry in Scan(src, !skipExpandForDoubleQuotes, !skipConvertingWindowsNewlines))
        {
            if (collectAllValues)
            {
                // handle scenario where user mistakenly includes plaintext duplicate in .env
                obj.TryAdd(entry.Key, entry.Value);
            }
            else
            {
                obj[entry.Key] = entry.Value;
            }
        }

        return obj;
    }
}
