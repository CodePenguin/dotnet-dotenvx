using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/escapeForRegex.js</remarks>
public static partial class Helpers
{
    private static string EscapeForRegex(string str)
    {
        return Regex.Escape(str).Replace("-", @"\x2d");
    }
}