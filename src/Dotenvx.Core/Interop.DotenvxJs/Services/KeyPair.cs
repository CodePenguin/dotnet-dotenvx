using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/keypair.js (moved there from
/// src/lib/services/keypair.js, which no longer exists at this pin - confirmed 2026-09-24, part of
/// the same custodian/keyring reorg CLAUDE.md already documents elsewhere).
///
/// This class is a dual-purpose seam: the `keypair` CLI command uses it directly (via
/// KeyPairAction.cs), but `FindPrivateKey.cs`/`FindPublicKey.cs` ALSO use it internally as an
/// "does a key already exist for this file" lookup - called by run/encrypt/set's fresh-key-
/// generation logic for env files that legitimately don't exist YET (e.g. `encrypt` on a brand
/// new file creates a sample kit first, checking for an existing key before doing so). A missing-
/// file existence check therefore CANNOT live here without breaking those callers (confirmed the
/// hard way 2026-09-25: throwing here made `encrypt`/`config()` on a missing file fail instead of
/// creating a fresh sample kit, since they route through this exact class to check for an existing
/// key first) - see KeyPairAction.cs for where the CLI-specific missing-file behavior actually
/// belongs instead.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class KeyPair(string envFile = ".env", string[]? envKeysFilePaths = null)
{
    private static readonly Func<string, string[]?, KeyPair> DefaultFactory =
        (envFile, envKeysFile) => new KeyPair(envFile, envKeysFile);

    private readonly string[] _envFiles = [envFile];

    protected KeyPair() : this(".env")
    {
        // For unit tests
    }

    public static Func<string, string[]?, KeyPair> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }


    public virtual Dictionary<string, string?> Run()
    {
        Dictionary<string, string?> result = new();

        foreach (string envFilePath in _envFiles)
        {
            // keypair.js: the public key is `publickeys(src)[0]`; a missing file is treated as "no key yet"
            // here (see the remarks above), the CLI action reports it before getting this far
            string? publicKey = Fsx.ExistsSync(envFilePath)
                ? PublicKeys(Fsx.ReadFileX(envFilePath)).FirstOrDefault()
                : null;
            if (string.IsNullOrEmpty(publicKey))
            {
                publicKey = null;
            }

            Dictionary<string, string> ring = new();
            if (publicKey != null)
            {
                ring[publicKey] = "";
            }

            // keypair.js: `fk = options.envKeysFilepath || path.resolve(path.dirname(filepath), '.env.keys')`
            string[] fk = envKeysFilePaths is { Length: > 0 }
                ? envKeysFilePaths
                : [Path.Combine(Path.GetDirectoryName(Path.GetFullPath(envFilePath)) ?? "", ".env.keys")];

            ring = KeyringSync(new KeyringOptions
            {
                Ring = ring,
                Fk = fk,
                ProcessEnv = EnvironmentHelper.GetEnvironmentVariablesAsDictionary(),
                ReadFile = path => Fsx.ReadFileX(path)
            });

            result[GuessPublicKeyName(envFilePath)] = publicKey;
            result[GuessPrivateKeyName(envFilePath)] =
                publicKey != null && !string.IsNullOrEmpty(ring.GetValueOrDefault(publicKey))
                    ? ring[publicKey]
                    : null;
        }

        return result;
    }
}
