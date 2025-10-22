using Dotenvx.Core.Interop.DotenvxJs.Services;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static string? FindPrivateKey(string envFilepath, string? envKeysFilepath = null, bool opsOn = true)
    {
        // use path/to/.env.${environment} to generate privateKeyName
        string privateKeyName = GuessPrivateKeyName(envFilepath);

        Dictionary<string, string?> proKeypairs =
            opsOn
                ? new ProKeyPair(envFilepath).Run() // TODO: implement custom envKeysFilepath
                : new Dictionary<string, string?>();
        Dictionary<string, string> keypairs = new KeyPair(envFilepath, envKeysFilepath).Run();

        return proKeypairs.GetValueOrDefault(privateKeyName) ?? keypairs.GetValueOrDefault(privateKeyName);
    }
}