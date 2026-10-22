namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/guessPrivateKeyName.js</remarks>
internal static partial class Helpers
{
    public static string GuessPrivateKeyName(string envFilePath)
    {
        string filename = CanonicalEnvFilename(envFilePath);

        // .env
        if (filename == ".env")
        {
            return "DOTENV_PRIVATE_KEY";
        }

        // .env.ENVIRONMENT
        string environment = GuessEnvironment(envFilePath);

        return $"DOTENV_PRIVATE_KEY_{JsCase.ToUpper(environment)}";
    }
}
