using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/chomp.js</remarks>
public static partial class Helpers
{
    public static string Chomp(string value)
    {
        return TrailingNewlinesRegex().Replace(value, "");
    }

    [GeneratedRegex(@"[\r\n]+$")]
    private static partial Regex TrailingNewlinesRegex();
}
