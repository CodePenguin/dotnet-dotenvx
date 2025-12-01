using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public class ProKeyPair(string envFilePath)
{
    public Dictionary<string, string?> Run()
    {
        // Since we can't run external commands in C#, implement the fallback logic
        string privateKeyName = GuessPrivateKeyName(envFilePath);
        string publicKeyName = GuessPublicKeyName(envFilePath);

        // match format of dotenvx-pro
        return new Dictionary<string, string?> { [privateKeyName] = null, [publicKeyName] = null };
    }
}