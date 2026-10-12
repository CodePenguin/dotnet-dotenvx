using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/escapeForRegex.js. A generic, dependency-free regex-
/// escaping utility needed by both this project's own Upsert() and Dotenvx.Core's Append.cs (a
/// dotenvx-own helper, unrelated to @dotenvx/primitives) - living here since Core already
/// references this project for Upsert/Encrypt/etc.
/// </remarks>
public static partial class Primitives
{
    public static string EscapeForRegex(string str)
    {
        return Regex.Escape(str).Replace("-", @"\x2d");
    }
}
