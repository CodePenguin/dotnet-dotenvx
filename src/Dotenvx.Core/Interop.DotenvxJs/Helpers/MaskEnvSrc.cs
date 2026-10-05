using Dotenvx.Primitives;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/maskEnvSrc.js: `scan` with its defaults (so double-quoted escapes are
/// expanded before masking, which decides the masked length) and one `upsert` per key with the masked version of
/// EVERY occurrence, not just the last.
/// </remarks>
public static partial class Helpers
{
    public static string MaskEnvSrc(string envSrc, object? showChar = null)
    {
        ScanResult scanned = Scan(envSrc, new ScanOptions());

        foreach ((string key, List<string> values) in scanned.Parsed)
        {
            envSrc = Upsert(envSrc, key, values.Select(value => Mask(value, showChar)).ToArray());
        }

        return envSrc;
    }
}
