using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/keypair.js (moved there from
/// src/lib/services/keypair.js, which no longer exists at this pin - confirmed 2026-09-24, part of
/// the same custodian/keyring reorg CLAUDE.md already documents elsewhere).
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class KeyPair(string envFile = ".env", string? envKeysFilePath = null)
{
    private static readonly Func<string, string?, KeyPair> DefaultFactory =
        (envFile, envKeysFile) => new KeyPair(envFile, envKeysFile);

    private readonly string[] _envFiles = [envFile];

    protected KeyPair() : this(".env")
    {
        // For unit tests
    }

    public static Func<string, string?, KeyPair> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual Dictionary<string, string?> Run()
    {
        Dictionary<string, string?> result = new();

        foreach (string envFilePath in _envFiles)
        {
            // public key
            string publicKeyName = GuessPublicKeyName(envFilePath);
            string? publicKeyValue = SmartDotenvPublicKey(envFilePath);
            result[publicKeyName] = publicKeyValue;

            // private key
            string privateKeyName = GuessPrivateKeyName(envFilePath);
            string? privateKeyValue = SmartDotenvPrivateKey(envFilePath, envKeysFilePath);
            result[privateKeyName] = privateKeyValue;
        }

        return result;
    }
}