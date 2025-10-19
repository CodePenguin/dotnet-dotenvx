namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    internal static string GuessPrivateKeyName(string envFilepath)
    {
        string filename = Path.GetFileName(envFilepath).ToLower();

        // .env
        if (filename == ".env")
        {
            return "DOTENV_PRIVATE_KEY";
        }

        // .env.ENVIRONMENT
        string environment = GuessEnvironment(envFilepath);

        return $"DOTENV_PRIVATE_KEY_{environment.ToUpper()}";
    }
}