namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `keyring` / `keyringSync` exports (read live from the
/// minified source). Builds a ring of publicKeyHex -> privateKeyHex: every DOTENV_PRIVATE_KEY* value
/// from the `fk` key files (comma lists split; an unreadable file contributes nothing, default
/// `.env.keys`) and from processEnv is derived into its public key; every DOTENV_PUBLIC_KEY* value in
/// processEnv gets an empty slot if absent; then `provider(publicKeyHex)` fills still-empty slots.
/// </remarks>
public static partial class Primitives
{
    public static Dictionary<string, string> KeyringSync(KeyringOptions? options = null)
    {
        options ??= new KeyringOptions();
        List<string> privateKeys = [];
        foreach (string file in options.Fk ?? [".env.keys"])
        {
            privateKeys.AddRange(ReadPrivateKeys(file, options.ReadFile));
        }

        return BuildKeyring(options, privateKeys, key => options.Provider?.Invoke(key));
    }

    public static async Task<Dictionary<string, string>> Keyring(KeyringOptions? options = null)
    {
        options ??= new KeyringOptions();
        List<string> privateKeys = [];
        foreach (string file in options.Fk ?? [".env.keys"])
        {
            privateKeys.AddRange(await Task.Run(() => ReadPrivateKeys(file, options.ReadFile)));
        }

        Dictionary<string, string> ring = BuildKeyring(options, privateKeys, null);
        if (options.AsyncProvider is null)
        {
            return ring;
        }

        foreach (string key in ring.Keys.ToList())
        {
            if (!string.IsNullOrEmpty(ring[key]))
            {
                continue;
            }

            IDictionary<string, string>? found = await options.AsyncProvider(key);
            MergeProvided(ring, found);
        }

        return ring;
    }

    private static Dictionary<string, string> BuildKeyring(KeyringOptions options, List<string> privateKeys,
        Func<string, IDictionary<string, string>?>? provider)
    {
        Dictionary<string, string> ring = options.Ring ?? new Dictionary<string, string>();
        IDictionary<string, string> env = options.ProcessEnv ?? CurrentProcessEnv();

        foreach (KeyValuePair<string, string> kv in env)
        {
            if (kv.Key.StartsWith("DOTENV_PUBLIC_KEY") && string.IsNullOrEmpty(ring.GetValueOrDefault(kv.Value)))
            {
                ring[kv.Value] = "";
            }

            if (kv.Key.StartsWith("DOTENV_PRIVATE_KEY"))
            {
                privateKeys.Add(kv.Value);
            }
        }

        foreach (string privateKey in privateKeys)
        {
            try
            {
                ring[Derive(privateKey)] = privateKey;
            }
            catch
            {
                // an unusable private key is skipped
            }
        }

        if (provider is not null)
        {
            foreach (string key in ring.Keys.ToList())
            {
                if (!string.IsNullOrEmpty(ring[key]))
                {
                    continue;
                }

                MergeProvided(ring, provider(key));
            }
        }

        return ring;
    }

    private static void MergeProvided(Dictionary<string, string> ring, IDictionary<string, string>? found)
    {
        foreach (KeyValuePair<string, string> kv in found ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                ring[kv.Key] = kv.Value;
            }
        }
    }

    private static List<string> ReadPrivateKeys(string path, Func<string, string>? readFile)
    {
        List<string> keys = [];
        try
        {
            string src = readFile is null ? File.ReadAllText(path) : readFile(path);
            foreach (ScanEntry entry in Scan(src))
            {
                if (entry.Key.StartsWith("DOTENV_PRIVATE_KEY"))
                {
                    keys.AddRange(entry.Value.Split(','));
                }
            }
        }
        catch
        {
            return [];
        }

        return keys;
    }

    internal static Dictionary<string, string> CurrentProcessEnv()
    {
        Dictionary<string, string> env = new();
        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
        {
            env[(string)de.Key] = (string?)de.Value ?? "";
        }

        return env;
    }
}

public sealed class KeyringOptions
{
    public Dictionary<string, string>? Ring { get; init; }
    public string[]? Fk { get; init; }
    public IDictionary<string, string>? ProcessEnv { get; init; }
    public Func<string, IDictionary<string, string>?>? Provider { get; init; }
    public Func<string, Task<IDictionary<string, string>?>>? AsyncProvider { get; init; }

    // .NET test seam, not part of the JS API: how key files are read (default File.ReadAllText); a throw means "unreadable"
    public Func<string, string>? ReadFile { get; init; }
}
