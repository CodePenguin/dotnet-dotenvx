using System.Text;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Interop.DotenvxJs.Services;

public record LockedPrivateKey(string PrivateKeyName, string LockedPrivateKeyValue, string PublicKeyValue);

public record PlainPrivateKey(string PrivateKeyName, string PrivateKeyValue, string PublicKeyValue);

public record LockResult(
    bool Changed,
    string PrivateKeyName,
    string? PrivateKeyValue,
    string? LockedPrivateKeyValue,
    string PublicKeyValue,
    bool AlreadyLocked = false);

public record LockUpPlan(PlainPrivateKey[] Matches, LockedPrivateKey[] AlreadyLocked);

public record LockRunResult(bool Changed, LockResult[] Results);

/// <remarks>Reference source: dotenvx/src/lib/services/lockUp.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class LockUp(string? envFile = null, string? envKeysFile = null)
{
    private static readonly Func<string?, string?, LockUp> DefaultFactory = (envFile, envKeysFile) =>
        new LockUp(envFile, envKeysFile);

    private readonly string _envFile = envFile ?? ".env";
    private readonly string _envKeysFile = envKeysFile ?? ".env.keys";

    protected LockUp() : this(null, null)
    {
        // For unit tests
    }

    public static Func<string?, string?, LockUp> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    /// <summary>lockUp.js's latestValues: the last value of every scanned key, in key order.</summary>
    internal static List<KeyValuePair<string, string>> LatestValues(string src)
    {
        return [.. Primitives.Primitives.Scan(src, new ScanOptions()).Parsed
            .Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value[^1]))];
    }

    internal static Dictionary<string, PlainPrivateKey> PrivateKeyring(string keysSrc)
    {
        var keyring = new Dictionary<string, PlainPrivateKey>();

        foreach ((string privateKeyName, string privateKey) in LatestValues(keysSrc))
        {
            if (!privateKeyName.StartsWith("DOTENV_PRIVATE_KEY"))
            {
                continue;
            }

            try
            {
                string publicKey = Primitives.Primitives.Derive(privateKey);
                keyring[publicKey] = new PlainPrivateKey(privateKeyName, privateKey, publicKey);
            }
            catch
            {
                // lockUp.js: `catch {}`
            }
        }

        return keyring;
    }

    internal static Dictionary<string, LockedPrivateKey> LockedKeyring(string keysSrc)
    {
        var keyring = new Dictionary<string, LockedPrivateKey>();

        foreach ((string privateKeyName, string privateKey) in LatestValues(keysSrc))
        {
            if (!privateKeyName.StartsWith("DOTENV_PRIVATE_KEY") || !privateKey.StartsWith("locked:"))
            {
                continue;
            }

            string[] parts = privateKey.Split(':');
            if (parts.Length < 3)
            {
                continue;
            }

            keyring[parts[1]] = new LockedPrivateKey(privateKeyName, privateKey, parts[1]);
        }

        return keyring;
    }

    public virtual LockUpPlan Plan()
    {
        string envSrc = Encoding.UTF8.GetString(Fsx.ReadFileSync(_envFile));
        string keysSrc = Encoding.UTF8.GetString(Fsx.ReadFileSync(_envKeysFile));
        Dictionary<string, PlainPrivateKey> ring = PrivateKeyring(keysSrc);
        Dictionary<string, LockedPrivateKey> lockedRing = LockedKeyring(keysSrc);
        var matches = new List<PlainPrivateKey>();
        var alreadyLocked = new List<LockedPrivateKey>();

        foreach (string publicKey in Primitives.Primitives.PublicKeys(envSrc))
        {
            if (ring.TryGetValue(publicKey, out PlainPrivateKey? privateKey))
            {
                matches.Add(privateKey);
                continue;
            }

            if (lockedRing.TryGetValue(publicKey, out LockedPrivateKey? lockedPrivateKey))
            {
                alreadyLocked.Add(lockedPrivateKey);
            }
        }

        if (matches.Count < 1 && alreadyLocked.Count < 1)
        {
            throw new MissingKeyException("DOTENV_PRIVATE_KEY");
        }

        return new LockUpPlan([.. matches], [.. alreadyLocked]);
    }

    public virtual LockRunResult Run(Func<string, string, string> lockPrivateKey)
    {
        var results = new List<LockResult>();
        LockUpPlan plan = Plan();

        foreach (PlainPrivateKey privateKey in plan.Matches)
        {
            string locked = lockPrivateKey(privateKey.PrivateKeyValue, privateKey.PublicKeyValue);
            UpsertEnvKeyResult result = UpsertEnvKey(privateKey.PrivateKeyName, locked, _envKeysFile);

            results.Add(new LockResult(result.Changed, privateKey.PrivateKeyName, privateKey.PrivateKeyValue, locked,
                privateKey.PublicKeyValue));
        }

        foreach (LockedPrivateKey locked in plan.AlreadyLocked)
        {
            results.Add(new LockResult(false, locked.PrivateKeyName, null, locked.LockedPrivateKeyValue,
                locked.PublicKeyValue, true));
        }

        return new LockRunResult(results.Any(result => result.Changed), [.. results]);
    }
}
