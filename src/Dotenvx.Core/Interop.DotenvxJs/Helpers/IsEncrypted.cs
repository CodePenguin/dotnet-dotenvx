namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static bool IsEncrypted(string value)
    {
        return value.StartsWith(Prefix);
    }
}