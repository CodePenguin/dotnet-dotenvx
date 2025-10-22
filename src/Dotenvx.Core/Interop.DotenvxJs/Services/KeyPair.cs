namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public class KeyPair(string envFile = ".env", string? envKeysFilepath = null)
{
    private readonly string[] _envFiles = [envFile];

    public Dictionary<string, string> Run()
    {
        Dictionary<string, string> result = new();

        foreach (string envFilepath in _envFiles)
        {
            // public key
            string publicKeyName = Helpers.Helpers.GuessPublicKeyName(envFilepath);
            string? publicKeyValue = Helpers.Helpers.SmartDotenvPublicKey(envFilepath);
            if (publicKeyValue != null)
            {
                result[publicKeyName] = publicKeyValue;
            }

            // private key
            string privateKeyName = Helpers.Helpers.GuessPrivateKeyName(envFilepath);
            string? privateKeyValue = Helpers.Helpers.SmartDotenvPrivateKey(envFilepath, envKeysFilepath);
            if (privateKeyValue != null)
            {
                result[privateKeyName] = privateKeyValue;
            }
        }

        return result;
    }
}