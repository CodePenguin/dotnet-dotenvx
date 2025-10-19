namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public class KeyPair
{
    private readonly string[] _envFiles;
    private readonly string? _envKeysFilepath;

    public KeyPair(string envFile = ".env", string? envKeysFilepath = null)
    {
        _envFiles = [envFile];
        _envKeysFilepath = envKeysFilepath;
    }

    public KeyPair(string[] envFiles, string? envKeysFilepath = null)
    {
        _envFiles = envFiles;
        _envKeysFilepath = envKeysFilepath;
    }

    public Dictionary<string, string> Run()
    {
        Dictionary<string, string> result = new();

        foreach (string envFilepath in _envFiles)
        {
            // public key
            string publicKeyName = Helpers.GuessPublicKeyName(envFilepath);
            string? publicKeyValue = Helpers.SmartDotenvPublicKey(envFilepath);
            if (publicKeyValue != null)
            {
                result[publicKeyName] = publicKeyValue;
            }

            // private key
            string privateKeyName = Helpers.GuessPrivateKeyName(envFilepath);
            string? privateKeyValue = Helpers.SmartDotenvPrivateKey(envFilepath, _envKeysFilepath);
            if (privateKeyValue != null)
            {
                result[privateKeyName] = privateKeyValue;
            }
        }

        return result;
    }
}