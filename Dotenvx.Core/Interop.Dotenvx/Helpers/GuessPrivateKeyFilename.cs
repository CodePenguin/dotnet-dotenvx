namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static partial class Helpers
{
    internal static string GuessPrivateKeyFilename(string privateKeyName)
    {
        // TODO: Translate logic from guessPrivateKeyFilename.js
        return ".env"; // Return default for now
    }
}