using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/cryptography/isPlainKey.js</remarks>
public static partial class Helpers
{
    public static bool IsPlainKey(string key)
    {
        return PlainKeyRegex().IsMatch(key);
    }

    [GeneratedRegex("_PLAIN$")]
    private static partial Regex PlainKeyRegex();
}
