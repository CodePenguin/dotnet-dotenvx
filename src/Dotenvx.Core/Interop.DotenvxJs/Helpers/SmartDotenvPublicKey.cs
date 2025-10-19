namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static string? SmartDotenvPublicKey(string envFilepath)
    {
        string publicKeyName = GuessPublicKeyName(envFilepath); // DOTENV_PUBLIC_KEY_${ENVIRONMENT}

        // 1. attempt process.env first
        string? publicKey = SearchProcessEnv(publicKeyName);
        if (publicKey != null)
        {
            return publicKey;
        }

        // 2. attempt .env.keys second (path/to/.env.keys)
        publicKey = SearchEnvFile(publicKeyName, envFilepath);
        return publicKey;
    }

    private static string? SearchEnvFile(string publicKeyName, string envFilepath)
    {
        if (!File.Exists(envFilepath))
        {
            return null;
        }

        string keysSrc = File.ReadAllText(envFilepath);
        Dictionary<string, object> keysParsed = DotenvParse(keysSrc);

        if (keysParsed.TryGetValue(publicKeyName, out object? value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return null;
    }
}