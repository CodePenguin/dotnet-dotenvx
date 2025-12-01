namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/isPublicKey.js</remarks>
public static partial class Helpers
{
    public static bool IsPublicKey(string key, string _)
    {
        return key.StartsWith("DOTENV_PUBLIC_KEY");
    }
}