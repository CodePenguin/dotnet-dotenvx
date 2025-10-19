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
        var result = new Dictionary<string, string>();

        foreach (var envFilepath in _envFiles)
        {
            // public key
            var publicKeyName = Helpers.GuessPublicKeyName(envFilepath);
            var publicKeyValue = Helpers.SmartDotenvPublicKey(envFilepath);
            if (publicKeyValue != null)
            {
                result[publicKeyName] = publicKeyValue;
            }

            // private key
            var privateKeyName = Helpers.GuessPrivateKeyName(envFilepath);
            var privateKeyValue = Helpers.SmartDotenvPrivateKey(envFilepath, _envKeysFilepath);
            if (privateKeyValue != null)
            {
                result[privateKeyName] = privateKeyValue;
            }
        }

        return result;
    }
}