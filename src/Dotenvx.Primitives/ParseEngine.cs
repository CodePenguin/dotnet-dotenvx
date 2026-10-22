namespace Dotenvx.Primitives;

public sealed class PrimitivesParseOptions
{
    public bool Overload { get; init; }
    public string[]? Ik { get; init; }
    public string[]? Ek { get; init; }
    public IDictionary<string, string>? ProcessEnv { get; init; }
    public string[]? Fk { get; init; }
    public Func<string, IDictionary<string, string>?>? Provider { get; init; }
    public Func<string, Task<IDictionary<string, string>?>>? AsyncProvider { get; init; }

    // .NET test seam, not part of the JS API: how key files are read (default File.ReadAllText); a throw means "unreadable"
    public Func<string, string>? ReadFile { get; init; }

    /// <summary>parseWithDecryptor.js `parseOptionsWithoutProvider`: the same options with no key provider.</summary>
    public PrimitivesParseOptions WithoutProvider()
    {
        return new PrimitivesParseOptions
        {
            Overload = Overload,
            Ik = Ik,
            Ek = Ek,
            ProcessEnv = ProcessEnv,
            Fk = Fk,
            ReadFile = ReadFile
        };
    }
}

public sealed record PrimitivesError(string Code, string Message);

public sealed record PrimitivesParseOutput(
    Dictionary<string, string> Parsed,
    Dictionary<string, string> Injected,
    Dictionary<string, string> Existed,
    List<PrimitivesError> Errors);

public sealed record PrimitivesParseArraysOutput(
    Dictionary<string, List<string>> Parsed,
    Dictionary<string, List<string>> Injected,
    Dictionary<string, List<string>> Existed,
    List<PrimitivesError> Errors);

/// <remarks>
/// Counterpart of the @dotenvx/primitives `parse`, `parseSync`, `parsearrays` and `parsearraysSync` exports
/// (engine read live from the minified source, checked by PrimitivesOracleParityTest). Per entry: take the
/// pre-existing process env value unless overloading, decrypt it with the keyring (the key for the file's
/// public key first, then any other key; a failure leaves it encrypted and reports one combined
/// DECRYPTION_FAILED error), then evaluate `$(...)` and expand `${...}` unless single-quoted. The array
/// variants keep every occurrence of a key. Dotenvx's own engine in Core (Helpers/Parse.cs) is a different
/// layer: it decrypts with a single private key and reports specific per-key errors.
/// </remarks>
public static partial class Primitives
{
    public static PrimitivesParseOutput ParseSync(string? src, PrimitivesParseOptions? options = null)
    {
        options ??= new PrimitivesParseOptions();
        return ToLast(RunEngine(src, options, KeyringSync(RingOptions(src, options))));
    }

    public static PrimitivesParseArraysOutput ParseArraysSync(string? src, PrimitivesParseOptions? options = null)
    {
        options ??= new PrimitivesParseOptions();
        return RunEngine(src, options, KeyringSync(RingOptions(src, options)));
    }

    public static async Task<PrimitivesParseOutput> Parse(string? src, PrimitivesParseOptions? options)
    {
        options ??= new PrimitivesParseOptions();
        return ToLast(RunEngine(src, options, await Keyring(RingOptions(src, options))));
    }

    public static async Task<PrimitivesParseArraysOutput> ParseArrays(string? src,
        PrimitivesParseOptions? options)
    {
        options ??= new PrimitivesParseOptions();
        return RunEngine(src, options, await Keyring(RingOptions(src, options)));
    }

    private static KeyringOptions RingOptions(string? src, PrimitivesParseOptions options)
    {
        // the ring starts with an empty slot for every public key found in the source
        Dictionary<string, string> ring = new();
        foreach (string publicKey in PublicKeys(src ?? ""))
        {
            ring[publicKey] = "";
        }

        return new KeyringOptions
        {
            Ring = ring,
            Fk = options.Fk,
            ProcessEnv = options.ProcessEnv,
            ReadFile = options.ReadFile,
            Provider = options.Provider,
            AsyncProvider = options.AsyncProvider
        };
    }

    private static PrimitivesParseOutput ToLast(PrimitivesParseArraysOutput arrays)
    {
        return new PrimitivesParseOutput(Last(arrays.Parsed), Last(arrays.Injected), Last(arrays.Existed),
            arrays.Errors);
    }

    private static Dictionary<string, string> Last(Dictionary<string, List<string>> source)
    {
        return source.ToDictionary(kv => kv.Key, kv => kv.Value[^1]);
    }

    // non-array mode keeps only the latest value (`y[S]=K` / `x[S]=y[S]` / `B[S]=i[S]`)
    private static void Put(Dictionary<string, List<string>> dict, string key, string value)
    {
        if (!dict.TryGetValue(key, out List<string>? list))
        {
            dict[key] = list = [];
        }

        list.Add(value);
    }

    private static PrimitivesParseArraysOutput RunEngine(string? src, PrimitivesParseOptions options,
        Dictionary<string, string> ring)
    {
        bool overload = options.Overload;
        string[] ik = options.Ik ?? [];
        string[] ek = options.Ek ?? [];
        IDictionary<string, string> env = options.ProcessEnv ?? CurrentProcessEnv();
        string[] publicKeyHexes = PublicKeys(src ?? "");
        Func<string, bool> includes = Match(ik, ek);
        Func<string, bool> excludes = Match(ek);

        Dictionary<string, string> running = new();
        Dictionary<string, string> literals = new();
        Dictionary<string, List<string>> parsed = new();
        Dictionary<string, List<string>> injected = new();
        Dictionary<string, List<string>> existed = new();
        Dictionary<string, bool> undecrypted = new();

        foreach (ScanEntry entry in Scan(src))
        {
            string name = entry.Key;
            string k = entry.Value;
            bool has = env.ContainsKey(name);
            if (!overload && has)
            {
                k = env[name];
            }

            bool wasEncrypted = Encrypted(k);
            if (!excludes(name) && (ik.Length == 0 || includes(name)))
            {
                k = DecryptWithRing(ring, k, publicKeyHexes);
                if (wasEncrypted && Encrypted(k))
                {
                    undecrypted[name] = true;
                }
            }

            bool isEncrypted = Encrypted(k);
            bool evaled = false;
            if (!isEncrypted && entry.Quote != (char)39 && (!has || env[name] == k))
            {
                string before = k;
                try
                {
                    k = Evaluate(name, before, env, running);
                }
                catch
                {
                    // evaluation failures leave the value untouched
                }

                evaled = before != k;
            }

            if (!isEncrypted && !evaled && entry.Quote != (char)39 &&
                (!env.TryGetValue(name, out string? current) || string.IsNullOrEmpty(current) || overload))
            {
                k = Expand(k, overload, env, running, literals).Replace("\\$", "$");
            }

            if (entry.Quote == (char)39)
            {
                literals[name] = k;
            }

            running[name] = k;
            Put(parsed, name, k);

            bool preExisting = has && !overload;
            bool decryptedNow = preExisting && wasEncrypted && !Encrypted(k);
            if (preExisting && !decryptedNow)
            {
                Put(existed, name, env[name]);
            }
            else
            {
                Put(injected, name, k);
            }
        }

        List<PrimitivesError> errors = [];
        if (undecrypted.Count > 0)
        {
            errors.Add(new PrimitivesError("DECRYPTION_FAILED",
                "[DECRYPTION_FAILED] could not decrypt " + string.Join(", ", undecrypted.Keys)));
        }

        return new PrimitivesParseArraysOutput(parsed, injected, existed, errors);
    }

    private static string DecryptWithRing(Dictionary<string, string> ring, string value, string[] publicKeyHexes)
    {
        HashSet<string> tried = [];
        foreach (string publicKey in publicKeyHexes)
        {
            if (!ring.TryGetValue(publicKey, out string? privateKey) || string.IsNullOrEmpty(privateKey))
            {
                continue;
            }

            tried.Add(privateKey);
            try
            {
                return Decrypt(privateKey, value);
            }
            catch
            {
                // try the next key
            }
        }

        foreach (string privateKey in ring.Values.ToList())
        {
            if (string.IsNullOrEmpty(privateKey) || tried.Contains(privateKey))
            {
                continue;
            }

            try
            {
                return Decrypt(privateKey, value);
            }
            catch
            {
                // try the next key
            }
        }

        return value;
    }
}
