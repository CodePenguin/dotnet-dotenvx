namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static string GuessPrivateKeyFilename(string privateKeyName)
    {
        const string prefix = "DOTENV_PRIVATE_KEY";

        // .env
        if (privateKeyName == prefix)
        {
            return ".env";
        }

        // .env.ENVIRONMENT
        string suffix = privateKeyName[$"{prefix}_".Length..].Replace("_", ".").ToLower();
        return $".env.{suffix}";
    }
}