namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private const string PublicKeySchema = "DOTENV_PUBLIC_KEY";
    private const string PrivateKeySchema = "DOTENV_PRIVATE_KEY";

    public static string? SmartDotenvPrivateKey(string envFilepath, string? envKeysFilepath = null)
    {
        string? privateKeyName = GuessPrivateKeyName(envFilepath); // DOTENV_PRIVATE_KEY_${ENVIRONMENT}

        // 1. attempt process.env first
        string? privateKey = SearchProcessEnv(privateKeyName);
        if (privateKey != null)
        {
            return privateKey;
        }

        // 2. attempt .env.keys second (path/to/.env.keys)
        privateKey = SearchKeysFile(privateKeyName, envFilepath, envKeysFilepath);
        if (privateKey != null)
        {
            return privateKey;
        }

        // 3. attempt inverting `DOTENV_PUBLIC_KEY*` name inside file (unlocks custom filenames not matching .env.${ENVIRONMENT} pattern)
        privateKeyName = InvertForPrivateKeyName(envFilepath);
        if (privateKeyName == null)
        {
            return null;
        }

        // 3.1 attempt process.env first
        privateKey = SearchProcessEnv(privateKeyName);
        if (privateKey != null)
        {
            return privateKey;
        }

        // 3.2. attempt .env.keys second (path/to/.env.keys)
        privateKey = SearchKeysFile(privateKeyName, envFilepath, envKeysFilepath);
        return privateKey ?? null;
    }

    private static string? SearchKeysFile(string privateKeyName, string envFilepath, string? envKeysFilepath)
    {
        string keysFilepath = Path.Combine(Path.GetDirectoryName(envFilepath)!, ".env.keys"); // typical scenario
        if (!string.IsNullOrEmpty(envKeysFilepath))
        {
            // user specified
            keysFilepath = Path.GetFullPath(envKeysFilepath);
        }

        if (!File.Exists(keysFilepath))
        {
            return null;
        }

        string keysSrc = File.ReadAllText(keysFilepath);
        Dictionary<string, object> keysParsed = DotenvParse(keysSrc);

        if (keysParsed.TryGetValue(privateKeyName, out object? value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return null;
    }

    private static string? InvertForPrivateKeyName(string envFilepath)
    {
        if (!File.Exists(envFilepath))
        {
            return null;
        }

        string envSrc = File.ReadAllText(envFilepath);
        Dictionary<string, object> envParsed = DotenvParse(envSrc);

        string? publicKeyName = null;
        foreach (string keyName in envParsed.Keys)
        {
            if (keyName != PublicKeySchema && !keyName.StartsWith(PublicKeySchema))
            {
                continue;
            }

            publicKeyName = keyName; // find DOTENV_PUBLIC_KEY* in filename
            break;
        }

        return
            publicKeyName?.Replace(PublicKeySchema,
                PrivateKeySchema); // return inverted (DOTENV_PUBLIC_KEY* -> DOTENV_PRIVATE_KEY*) if found
    }
}