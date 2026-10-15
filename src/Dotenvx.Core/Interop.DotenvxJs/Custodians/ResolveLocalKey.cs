using System.Text;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>A locally held private key and how to remove it from where it was found (`Remove` re-checks it first).</summary>
public sealed class LocalKey
{
    public string? PublicKey { get; init; }
    public string? PrivateKey { get; init; }
    public string PrivateKeyName { get; init; } = "";
    public Action Remove { get; init; } = () => { };
}

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/resolveLocalKey.js. Returns the originating store with the key, so a move
/// only removes that source. Never looks up Armor itself: these operations upload a locally held private key.
/// </remarks>
public static class ResolveLocalKey
{
    private static string? Read(string filename)
    {
        try
        {
            return Encoding.UTF8.GetString(Fsx.ReadFileSync(filename));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string? Value(string? src, string name)
    {
        return src is null
            ? null
            : Primitives.Primitives.Scan(src, new ScanOptions()).Parsed.GetValueOrDefault(name)?.LastOrDefault();
    }

    public static LocalKey Resolve(string envFile = ".env", bool allowMissing = false)
    {
        string? src = Read(envFile);
        (string publicKeyName, string privateKeyName) = Keynames(envFile, src ?? "");
        string? publicKey = Value(src, publicKeyName);
        if (allowMissing && string.IsNullOrEmpty(publicKey))
        {
            throw src is null ? new MissingEnvFileException(envFile) : new MissingKeyException(publicKeyName);
        }

        LocalKey Result(string stored, Action remove, Func<string?> reread)
        {
            // A file-only push remains supported.
            if (string.IsNullOrEmpty(publicKey))
            {
                publicKey = Primitives.Primitives.Derive(stored);
            }

            if (!global::Dotenvx.Providers.MatchesStoredKey.Matches(publicKey, stored))
            {
                throw new Exception("private key does not match the .env public key");
            }

            return new LocalKey
            {
                PublicKey = publicKey,
                PrivateKey = stored,
                PrivateKeyName = privateKeyName,
                Remove = () =>
                {
                    if (reread() != stored)
                    {
                        throw new Exception("private key changed during upload; original storage was not modified");
                    }

                    remove();
                }
            };
        }

        string[] files =
        [
            .. new[]
            {
                NodePath.Resolve(Path.Combine(Path.GetDirectoryName(envFile) is { Length: > 0 } dir ? dir : ".", ".env.keys")),
                NodePath.Resolve(".env.keys")
            }.Distinct()
        ];
        foreach (string filename in files)
        {
            string? stored = Value(Read(filename), privateKeyName);
            if (!string.IsNullOrEmpty(stored))
            {
                return Result(stored, () => EnvKeyFiles.RemoveEnvKey(privateKeyName, filename),
                    () => Value(Read(filename), privateKeyName));
            }
        }

        if (!string.IsNullOrEmpty(publicKey))
        {
            foreach (string id in new[] { "native", "onepassword", "bitwarden" })
            {
                ICustodian custodian = Custodians.Get(id);
                if (!custodian.Enabled() || !custodian.Configured())
                {
                    continue;
                }

                string? stored = custodian.Get(publicKey).Lookup(publicKey);
                if (!string.IsNullOrEmpty(stored))
                {
                    string key = publicKey;

                    return Result(stored, () => ((ILocalCustodian)custodian).Delete(key),
                        () => custodian.Get(key).Lookup(key));
                }
            }
        }

        // Up can be repeated for a key already held by Armor; the server verifies it.
        if (allowMissing)
        {
            return new LocalKey { PublicKey = publicKey, PrivateKeyName = privateKeyName };
        }

        if (string.IsNullOrEmpty(publicKey) && src is null)
        {
            throw new MissingEnvFileException(".env.keys");
        }

        throw new Exception($"private key {privateKeyName} not found in .env.keys, OS secret storage, 1Password, or Bitwarden");
    }
}

internal static class CustodianLookup
{
    public static string? Lookup(this IDictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out string? value) ? value : null;
    }
}
