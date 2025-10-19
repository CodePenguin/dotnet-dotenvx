namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public class ProKeyPair(string envFilepath)
{
    public Dictionary<string, string?> Run()
    {
        // Since we can't run external commands in C#, implement the fallback logic
        string privateKeyName = Helpers.GuessPrivateKeyName(envFilepath);
        string publicKeyName = Helpers.GuessPublicKeyName(envFilepath);

        // match format of dotenvx-pro
        return new Dictionary<string, string?> { [privateKeyName] = null, [publicKeyName] = null };
    }
}