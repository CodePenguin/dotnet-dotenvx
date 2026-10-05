using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>The key-access switches every resolver takes: `noNative`, `noArmor` (`--no-armor`) and `token` (`--token`).</summary>
public class KeyAccessOptions
{
    public bool NoNative { get; set; }
    public bool NoArmor { get; set; }
    public string? Token { get; set; }
    public IDotenvxLogger? Logger { get; set; }

    /// <summary>The spinner status callback (`onStatus`): Armor access-approval instructions are reported through it.</summary>
    public Action<string>? OnStatus { get; set; }

    /// <summary>`options.command`: forwarded to the Armor provider/decryptor (unused by them, kept for parity).</summary>
    public string? Command { get; set; }

    /// <summary>`options.provider` given explicitly (even as null): returned as is, nothing is resolved.</summary>
    public bool HasCustomProvider { get; set; }

    public Func<string, IDictionary<string, string>?>? CustomProvider { get; set; }

    /// <summary>`options.decryptor` given explicitly (even as null).</summary>
    public bool HasCustomDecryptor { get; set; }

    public Decryptor? CustomDecryptor { get; set; }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/providers/index.js and providers/native/index.js. A provider is called with a public
/// key whose private key could not be found in the process environment or `.env.keys` and returns `{ publicKey:
/// privateKey }` (empty when it has nothing). The native provider reads the OS secret store; Armor (Phase E in
/// docs/parity-checklist.md) will be composed after it, in upstream's order.
/// </remarks>
public static class Providers
{
    /// <summary>`useNative(options)`: a supported platform, no `CI` variable and not disabled with --no-native.</summary>
    public static bool UseNative(bool noNative)
    {
        if (!NativeStore.IsSupportedPlatform)
        {
            return false;
        }

        // `if (process.env.CI) return false`: any non-empty value
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            return false;
        }

        return !noNative;
    }

    /// <summary>providers/native/index.js: any failure or a missing key is "nothing found".</summary>
    public static IDictionary<string, string> NativeProvider(string publicKeyHex)
    {
        if (!NativeStore.IsSupportedPlatform)
        {
            return new Dictionary<string, string>();
        }

        try
        {
            string? privateKeyHex = NativeStore.Current.Get(publicKeyHex);

            return string.IsNullOrEmpty(privateKeyHex)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [publicKeyHex] = privateKeyHex };
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static Func<string, IDictionary<string, string>?> Compose(
        List<Func<string, IDictionary<string, string>?>> providerFns)
    {
        // composeProviders: the first provider whose keyring has the key wins, else `{}`
        return publicKeyHex =>
        {
            foreach (Func<string, IDictionary<string, string>?> providerFn in providerFns)
            {
                IDictionary<string, string>? keyring = providerFn(publicKeyHex);
                if (keyring is not null && keyring.ContainsKey(publicKeyHex) && !string.IsNullOrEmpty(keyring[publicKeyHex]))
                {
                    return keyring;
                }
            }

            return new Dictionary<string, string>();
        };
    }

    /// <summary>`useArmor` / the `token` rule: `--token` forces Armor on, otherwise the session decides.</summary>
    private static bool ArmorEnabled(KeyAccessOptions options)
    {
        if (options.NoArmor)
        {
            return false;
        }

        bool noArmor = string.IsNullOrEmpty(options.Token) && new Session(options.Logger).NoArmor();

        return !noArmor;
    }

    /// <summary>
    /// `providers(options)`: the provider to pass to primitives (native first, then Armor, in upstream's order), or null
    /// when none applies (the caller then passes no provider at all, like envs.js' `options.provider = null`).
    /// </summary>
    public static Func<string, IDictionary<string, string>?>? Resolve(KeyAccessOptions options)
    {
        if (options.HasCustomProvider)
        {
            return options.CustomProvider;
        }

        var providerFns = new List<Func<string, IDictionary<string, string>?>>();

        if (UseNative(options.NoNative))
        {
            providerFns.Add(NativeProvider);
        }

        if (ArmorEnabled(options))
        {
            providerFns.Add(publicKeyHex => ArmorProviders.Provider(publicKeyHex, options.Logger, options.OnStatus));
        }

        return providerFns.Count switch
        {
            0 => null,
            1 => providerFns[0],
            _ => Compose(providerFns)
        };
    }

    /// <summary>`decryptors(options)`: the Armor server-side decryptor, or null when Armor is off.</summary>
    public static Decryptor? ResolveDecryptor(KeyAccessOptions options)
    {
        if (options.HasCustomDecryptor)
        {
            return options.CustomDecryptor;
        }

        return ArmorEnabled(options)
            ? (src, decryptOptions) => ArmorProviders.DecryptSrc(src,
                decryptOptions with { OnStatus = options.OnStatus, Token = options.Token, Command = options.Command },
                options.Logger)
            : null;
    }
}
