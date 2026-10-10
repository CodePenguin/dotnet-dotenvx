namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/dotenvPrivateKeyNames.js</remarks>
public static partial class Helpers
{
    public static string[] DotenvPrivateKeyNames(IDictionary<string, string> processEnv)
    {
        const string PrivateKeyNameSchema = "DOTENV_PRIVATE_KEY";
        return processEnv.Keys.Where(key => key.StartsWith(PrivateKeyNameSchema, StringComparison.Ordinal)).ToArray();
    }
}