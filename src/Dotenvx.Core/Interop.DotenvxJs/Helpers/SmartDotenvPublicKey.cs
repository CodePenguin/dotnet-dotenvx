namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/smartDotenvPublicKey.js</remarks>
public static partial class Helpers
{
    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    public static string? SmartDotenvPublicKey(string envFilePath)
    {
        string publicKeyName = GuessPublicKeyName(envFilePath); // DOTENV_PUBLIC_KEY_${ENVIRONMENT}

        // 1. attempt process.env first
        string? publicKey = SearchProcessEnv(publicKeyName);
        if (publicKey != null)
        {
            return publicKey;
        }

        // 2. attempt .env.keys second (path/to/.env.keys)
        publicKey = SearchEnvFile(publicKeyName, envFilePath);
        return publicKey;
    }

    private static string? SearchEnvFile(string publicKeyName, string envFilePath)
    {
        if (!Fsx.ExistsSync(envFilePath))
        {
            return null;
        }

        string keysSrc = Fsx.ReadFileX(envFilePath);
        Dictionary<string, object> keysParsed = DotenvParse(keysSrc);

        if (keysParsed.TryGetValue(publicKeyName, out object? value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return null;
    }
}