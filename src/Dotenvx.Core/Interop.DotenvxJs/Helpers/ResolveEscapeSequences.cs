namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/resolveEscapeSequences.js</remarks>
public static partial class Helpers
{
    public static string ResolveEscapeSequences(string value)
    {
        return value.Replace("\\$", "$");
    }
}