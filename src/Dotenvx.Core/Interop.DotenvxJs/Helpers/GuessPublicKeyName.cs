namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/guessPublicKeyName.js</remarks>
public static partial class Helpers
{
    public static string GuessPublicKeyName(string filepath)
    {
        string filename = CanonicalEnvFilename(filepath);

        // .env
        if (filename == ".env")
        {
            return "DOTENV_PUBLIC_KEY";
        }

        // .env.ENVIRONMENT
        string environment = GuessEnvironment(filename);

        return $"DOTENV_PUBLIC_KEY_{environment.ToUpperInvariant()}";
    }
}