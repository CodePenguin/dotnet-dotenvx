namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/findPrivateKey.js</remarks>
public static partial class Helpers
{
    public static string? FindPrivateKey(string envFilePath, string? envKeysFilePath = null, bool opsOn = true)
    {
        // use path/to/.env.${environment} to generate privateKeyName
        string privateKeyName = GuessPrivateKeyName(envFilePath);

        Dictionary<string, string?> proKeypairs =
            opsOn
                ? new ProKeyPair(envFilePath).Run() // TODO: implement custom envKeysFilePath
                : new Dictionary<string, string?>();
        Dictionary<string, string?> keypairs = Services.KeyPair.Factory(envFilePath, envKeysFilePath).Run();

        return proKeypairs.GetValueOrDefault(privateKeyName) ?? keypairs.GetValueOrDefault(privateKeyName);
    }
}