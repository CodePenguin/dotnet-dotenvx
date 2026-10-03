using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/smartDotenvPrivateKey.js</remarks>
public static partial class Helpers
{
    private const string PublicKeySchema = "DOTENV_PUBLIC_KEY";
    private const string PrivateKeySchema = "DOTENV_PRIVATE_KEY";

    public static string? SmartDotenvPrivateKey(string envFilePath, string? envKeysFilePath = null)
    {
        string? privateKeyName = GuessPrivateKeyName(envFilePath); // DOTENV_PRIVATE_KEY_${ENVIRONMENT}

        // 1. attempt process.env first
        string? privateKey = SearchProcessEnv(privateKeyName);
        if (privateKey != null)
        {
            return privateKey;
        }

        // 2. attempt .env.keys second (path/to/.env.keys)
        privateKey = SearchKeysFile(privateKeyName, envFilePath, envKeysFilePath);
        if (privateKey != null)
        {
            return privateKey;
        }

        // 3. attempt inverting `DOTENV_PUBLIC_KEY*` name inside file (unlocks custom filenames not matching .env.${ENVIRONMENT} pattern)
        privateKeyName = InvertForPrivateKeyName(envFilePath);
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
        privateKey = SearchKeysFile(privateKeyName, envFilePath, envKeysFilePath);
        return privateKey ?? null;
    }

    private static string? SearchKeysFile(string privateKeyName, string envFilePath, string? envKeysFilePath)
    {
        string keysFilePath = Path.Combine(Path.GetDirectoryName(envFilePath)!, ".env.keys"); // typical scenario
        if (!string.IsNullOrEmpty(envKeysFilePath))
        {
            // user specified
            keysFilePath = Path.GetFullPath(envKeysFilePath);
        }

        if (!Fsx.ExistsSync(keysFilePath))
        {
            return null;
        }

        string keysSrc = Fsx.ReadFileX(keysFilePath);
        Dictionary<string, object> keysParsed = ScanToDictionary(keysSrc);

        if (keysParsed.TryGetValue(privateKeyName, out object? value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return null;
    }

    private static string? InvertForPrivateKeyName(string envFilePath)
    {
        if (!Fsx.ExistsSync(envFilePath))
        {
            return null;
        }

        string envSrc = Fsx.ReadFileX(envFilePath);
        Dictionary<string, object> envParsed = ScanToDictionary(envSrc);

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