namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/guessPrivateKeyFilename.js</remarks>
public static partial class Helpers
{
    internal static string GuessPrivateKeyFilename(string privateKeyName)
    {
        const string prefix = "DOTENV_PRIVATE_KEY";

        // .env
        if (privateKeyName == prefix)
        {
            return ".env";
        }

        // .env.ENVIRONMENT
        // `substring` clamps to the string's end: DOTENV_PRIVATE_KEYS (no underscore) has an empty suffix
        int start = $"{prefix}_".Length;
        string suffix = (privateKeyName.Length > start ? privateKeyName[start..] : "").Replace("_", ".");
        suffix = JsCase.ToLower(suffix);
        return $".env.{suffix}";
    }
}