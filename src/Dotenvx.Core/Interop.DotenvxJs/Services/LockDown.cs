using System.Text;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Interop.DotenvxJs.Services;

public record LockDownPlan(LockedPrivateKey[] Locked, LockResult[] AlreadyUnlocked);

/// <remarks>Reference source: dotenvx/src/lib/services/lockDown.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class LockDown(string? envFile = null, string? envKeysFile = null)
{
    private static readonly Func<string?, string?, LockDown> DefaultFactory = (envFile, envKeysFile) =>
        new LockDown(envFile, envKeysFile);

    private readonly string _envFile = envFile ?? ".env";
    private readonly string _envKeysFile = envKeysFile ?? ".env.keys";

    protected LockDown() : this(null, null)
    {
        // For unit tests
    }

    public static Func<string?, string?, LockDown> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    internal static LockedPrivateKey[] LockedCandidates(string keysSrc)
    {
        var candidates = new List<LockedPrivateKey>();

        foreach ((string privateKeyName, string privateKey) in LockUp.LatestValues(keysSrc))
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

            candidates.Add(new LockedPrivateKey(privateKeyName, privateKey, parts[1]));
        }

        return [.. candidates];
    }

    public virtual LockDownPlan Plan()
    {
        string envSrc = Encoding.UTF8.GetString(Fsx.ReadFileSync(_envFile));
        string keysSrc = Encoding.UTF8.GetString(Fsx.ReadFileSync(_envKeysFile));
        var lockedByPublicKey = new Dictionary<string, LockedPrivateKey>();
        Dictionary<string, PlainPrivateKey> plaintextByPublicKey = LockUp.PrivateKeyring(keysSrc);
        var locked = new List<LockedPrivateKey>();
        var alreadyUnlocked = new List<LockResult>();

        foreach (LockedPrivateKey lockedPrivateKey in LockedCandidates(keysSrc))
        {
            lockedByPublicKey[lockedPrivateKey.PublicKeyValue] = lockedPrivateKey;
        }

        foreach (string publicKey in Primitives.Primitives.PublicKeys(envSrc))
        {
            if (lockedByPublicKey.TryGetValue(publicKey, out LockedPrivateKey? lockedPrivateKey))
            {
                locked.Add(lockedPrivateKey);
                continue;
            }

            if (plaintextByPublicKey.TryGetValue(publicKey, out PlainPrivateKey? privateKey))
            {
                alreadyUnlocked.Add(new LockResult(false, privateKey.PrivateKeyName, privateKey.PrivateKeyValue, null,
                    publicKey));
            }
        }

        if (locked.Count < 1 && alreadyUnlocked.Count < 1)
        {
            throw new MissingKeyException("DOTENV_PRIVATE_KEY");
        }

        return new LockDownPlan([.. locked], [.. alreadyUnlocked]);
    }

    public virtual LockRunResult Run(Func<string, string>? unlockPrivateKey)
    {
        LockDownPlan plan = Plan();
        var results = new List<LockResult>(plan.AlreadyUnlocked);

        if (plan.Locked.Length > 0 && unlockPrivateKey is null)
        {
            throw new InvalidOperationException("missing unlock private key function");
        }

        foreach (LockedPrivateKey lockedPrivateKey in plan.Locked)
        {
            string privateKeyValue;

            try
            {
                privateKeyValue = unlockPrivateKey!(lockedPrivateKey.LockedPrivateKeyValue);
            }
            catch
            {
                throw new InvalidPassphraseException(lockedPrivateKey.PrivateKeyName);
            }

            string publicKeyValue = Primitives.Primitives.Derive(privateKeyValue);

            if (publicKeyValue != lockedPrivateKey.PublicKeyValue)
            {
                continue;
            }

            UpsertEnvKeyResult result = UpsertEnvKey(lockedPrivateKey.PrivateKeyName, privateKeyValue, _envKeysFile);

            results.Add(new LockResult(result.Changed, lockedPrivateKey.PrivateKeyName, privateKeyValue,
                lockedPrivateKey.LockedPrivateKeyValue, publicKeyValue));
        }

        if (results.Count < 1)
        {
            throw new MissingKeyException("DOTENV_PRIVATE_KEY");
        }

        return new LockRunResult(results.Any(result => result.Changed), [.. results]);
    }
}
