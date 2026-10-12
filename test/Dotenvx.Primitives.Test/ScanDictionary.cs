using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Test glue: ordered `Scan` entries as a last-value-wins dictionary.</remarks>
internal static class ScanDictionary
{
    public static Dictionary<string, object> Of(string src, bool skipExpand = false, bool skipConvert = false)
    {
        Dictionary<string, object> obj = new();
        foreach (ScanEntry entry in Scan(src, !skipExpand, !skipConvert))
        {
            obj[entry.Key] = entry.Value;
        }

        return obj;
    }
}
