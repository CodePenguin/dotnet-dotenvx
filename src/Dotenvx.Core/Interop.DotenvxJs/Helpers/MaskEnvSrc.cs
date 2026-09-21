namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/maskEnvSrc.js</remarks>
public static partial class Helpers
{
    public static string MaskEnvSrc(string envSrc, object? showChar = null)
    {
        Dictionary<string, object> parsed = DotenvParse(envSrc, true, true);

        foreach (KeyValuePair<string, object> pair in parsed)
        {
            string masked = Mask((string)pair.Value, showChar);
            envSrc = Replace(envSrc, pair.Key, masked);
        }

        return envSrc;
    }
}
