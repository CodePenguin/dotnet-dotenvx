namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static string ResolveEscapeSequences(string value)
    {
        return value.Replace("\\$", "$");
    }
}