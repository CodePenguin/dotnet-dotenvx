namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/isEncrypted.js</remarks>
public static partial class Helpers
{
    public static bool IsEncrypted(string? value)
    {
        return value?.StartsWith(Prefix) == true;
    }
}