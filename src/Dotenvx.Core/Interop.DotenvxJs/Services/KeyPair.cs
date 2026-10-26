using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Primitives;
using Dotenvx.Providers;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/keypair.js (moved there from
/// src/lib/services/keypair.js, which no longer exists at this pin - confirmed 2026-09-24, part of
/// the same custodian/keyring reorg docs/porting-traps.md already documents elsewhere).
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
internal class KeyPair(string envFile = ".env", string[]? envKeysFilePaths = null)
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


    /// <summary>How missing private keys are looked up: --no-native, --no-armor, --token (providers/index.js options).</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new();

    public bool NoNative
    {
        get => KeyAccess.NoNative;
        set => KeyAccess.NoNative = value;
    }

    /// <summary>
    /// `onError`: receives a lookup error (an Armor failure, a key held only in an enclave) instead of it being thrown; the key is
    /// then reported as not found.
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>Where the private key came from in the last <see cref="Run" /> (`origins[filepath]`): null when there was none.</summary>
    public string? Origin { get; private set; }

    public virtual Dictionary<string, string?> Run()
    {
        Dictionary<string, string?> result = [];

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

            Dictionary<string, string> ring = [];
            if (publicKey != null)
            {
                ring[publicKey] = "";
            }

            // keypair.js: `fk = options.envKeysFilepath || path.resolve(path.dirname(filepath), '.env.keys')`
            string[] fk = envKeysFilePaths is { Length: > 0 }
                ? envKeysFilePaths
                : [Path.Combine(Path.GetDirectoryName(NodePath.Resolve(envFilePath)) ?? "", ".env.keys")];

            // the key files are tried first, then the providers; whichever answers is the origin of the key
            string origin = "environment";
            bool confirmedProvider = false;
            Action<string, string>? onProvider = KeyAccess.OnProvider;
            KeyAccess.OnProvider = (name, _) =>
            {
                origin = name;
                confirmedProvider = true;
            };
            Func<string, IDictionary<string, string>?>? provider = KeyProviders.Resolve(KeyAccess);

            FileProvider keys = new(new ProviderOptions { Fk = fk });
            Func<string, IDictionary<string, string>?> lookup = key =>
            {
                IDictionary<string, string> found = keys.GetSync(key);
                if (found.TryGetValue(key, out string? have) && !string.IsNullOrEmpty(have))
                {
                    origin = ".env.keys";

                    return found;
                }

                origin = "custom";

                return provider is null ? new Dictionary<string, string>() : provider(key);
            };

            try
            {
                ring = KeyringSync(new KeyringOptions
                {
                    Ring = ring,
                    Fk = publicKey != null ? [] : fk,
                    ProcessEnv = EnvironmentHelper.GetEnvironmentVariablesAsDictionary(),
                    ReadFile = path => Fsx.ReadFileX(path),
                    Provider = lookup
                });
            }
            catch (Exception error)
            {
                EnclavedHint.Apply(error, envFilePath);
                if (OnError is null)
                {
                    throw;
                }

                OnError(error);
                ring = [];
            }
            finally
            {
                KeyAccess.OnProvider = onProvider;
            }

            result[GuessPublicKeyName(envFilePath)] = publicKey;
            bool hasKey = publicKey != null && !string.IsNullOrEmpty(ring.GetValueOrDefault(publicKey));
            result[GuessPrivateKeyName(envFilePath)] = hasKey ? ring[publicKey!] : null;
            Origin = hasKey || (publicKey != null && confirmedProvider) ? origin : null;
        }

        return result;
    }
}
