using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/maskEnvSrc.js</remarks>
public static partial class Helpers
{
    public static string MaskEnvSrc(string envSrc, object? showChar = null)
    {
        Dictionary<string, object> parsed = Parse(envSrc, true, true);

        foreach (KeyValuePair<string, object> pair in parsed)
        {
            string masked = Mask((string)pair.Value, showChar);
            envSrc = Upsert(envSrc, pair.Key, masked);
        }

        return envSrc;
    }
}
