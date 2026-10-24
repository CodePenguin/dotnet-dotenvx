using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>
/// The key-access switches every resolver takes: `noNative`, `noArmor` (`--no-armor`), `token` (`--token`) and the password
/// manager switches.
/// </summary>
internal class KeyAccessOptions
{
    public bool NoNative { get; set; }
    private bool _noArmor;
    private Func<bool>? _noArmorSource;

    /// <summary>
    /// `noArmor`. When it is decided by the Armor session, that is asked (and `armor: on|off` logged) the first time something
    /// reads it, which is where upstream asks it.
    /// </summary>
    public bool NoArmor
    {
        get
        {
            if (_noArmorSource is { } source)
            {
                _noArmorSource = null;
                _noArmor = source();
            }

            return _noArmor;
        }
        set
        {
            _noArmorSource = null;
            _noArmor = value;
        }
    }

    /// <summary>Leaves `noArmor` to the session, asked on first use.</summary>
    public void DeferNoArmor(Func<bool> source)
    {
        _noArmorSource = source;
    }
    public bool No1Password { get; set; }
    public bool NoBitwarden { get; set; }
    public string? Token { get; set; }
    public IDotenvxLogger? Logger { get; set; }

    /// <summary>The spinner status callback (`onStatus`): Armor access-approval instructions are reported through it.</summary>
    public Action<string>? OnStatus { get; set; }

    /// <summary>`options.command`: forwarded to the Armor provider/decryptor (unused by them, kept for parity).</summary>
    public string? Command { get; set; }

    /// <summary>`options.onProvider(name, publicKey)`: which provider supplied each private key.</summary>
    public Action<string, string>? OnProvider { get; set; }

    /// <summary>The async resolver variant (run/get/keypair): a locked Bitwarden vault may be unlocked with a prompt.</summary>
    public bool Interactive { get; set; }

    /// <summary>`options.provider` given explicitly (even as null): returned as is, nothing is resolved.</summary>
    public bool HasCustomProvider { get; set; }

    public Func<string, IDictionary<string, string>?>? CustomProvider { get; set; }

    /// <summary>`options.decryptor` given explicitly (even as null).</summary>
    public bool HasCustomDecryptor { get; set; }

    public Decryptor? CustomDecryptor { get; set; }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/providers/index.js and decryptors/index.js. A provider is called with a public key whose
/// private key could not be found in the process environment or `.env.keys` and returns `{ publicKey: privateKey }` (empty
/// when it has nothing). The lookup is the @dotenvx/providers chain: key files, the OS secret store, Armor, 1Password and
/// Bitwarden, in that order.
/// </remarks>
internal static class KeyProviders
{
    internal static readonly Dictionary<string, string> NativeNames = new()
    {
        ["darwin"] = "macosKeychain",
        ["win32"] = "windowsCredentialManager",
        ["linux"] = "linuxSecretService"
    };

    /// <summary>`useNative(options)`: a supported platform, no `CI` variable and not disabled with --no-native.</summary>
    public static bool UseNative(bool noNative)
    {
        return new NativeCustodian().Enabled(new KeyAccessOptions { NoNative = noNative });
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
                ? []
                : new Dictionary<string, string> { [publicKeyHex] = privateKeyHex };
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>The options the providers take, from the key-access switches (`...options` in providers/index.js).</summary>
    private static ProviderOptions ChainOptions(KeyAccessOptions options, bool sync, string publicKey,
        Action<string> onLookup, Action<string> onLookupEnd, Action<Exception> onArmorError)
    {
        return new ProviderOptions
        {
            Fk = [],
            ProcessEnv = BitwardenCustodian.CommandEnv(),
            CliVersion = PackageJson.Version,
            NoNative = options.NoNative || !NativeStore.IsSupportedPlatform,
            NativeGet = key => NativeStore.Current is NativeStore.WindowsCredentialManager windows
                ? windows.GetForProviders(key)
                : NativeStore.Current.Get(key),
            NoArmor = options.NoArmor,
            NoOnePassword = options.No1Password,
            // The async CLI fallback can interactively unlock Bitwarden. The shared synchronous path reads BW_SESSION
            // without prompting.
            NoBitwarden = sync ? options.NoBitwarden : true,
            Token = options.Token,
            OnLookup = onLookup,
            OnLookupEnd = onLookupEnd,
            OnArmorError = onArmorError,
            OnApprovalRequired = options.OnStatus is null
                ? null
                : request =>
                {
                    string display = ArmoredKeyDisplay.Display(publicKey);
                    options.OnStatus(
                        $"[{request.Code}] press Enter to open [{request.ApprovalUri}] and approve{(display != "" ? $" ({display})" : "")}");
                    _cleanupOpenKeyListener = ListenForOpenKey.Listen(() => OpenUrl.Open(request.ApprovalUri));
                }
        };
    }

    [ThreadStatic] private static Action? _cleanupOpenKeyListener;

    private static bool IsEnclaved(Exception error)
    {
        return error is DotenvxException { Code: "ENCLAVED" } ||
               error is DotenvxException { Code: "BAD_REQUEST" } bad &&
               bad.Message is "[BAD_REQUEST] private key enclaved ◊" or
                   "[BAD_REQUEST] Private key is held exclusively in its remote enclave.";
    }

    private static IDictionary<string, string> Lookup(string publicKey, KeyAccessOptions options, bool sync)
    {
        string? source = null;
        Exception? armorError = null;
        _cleanupOpenKeyListener = null;

        void Cleanup()
        {
            _cleanupOpenKeyListener?.Invoke();
            _cleanupOpenKeyListener = null;
        }

        ProviderChain keys = new(ChainOptions(options, sync, publicKey, name => source = name, name =>
        {
            if (name == "armor")
            {
                Cleanup();
            }
        }, error => armorError = error));

        IDictionary<string, string> Found(IDictionary<string, string> ring)
        {
            bool has = ring.TryGetValue(publicKey, out string? value) && !string.IsNullOrEmpty(value);
            if (!has && armorError is not null)
            {
                // this response confirms custody even though the private key cannot be retrieved
                if (IsEnclaved(armorError))
                {
                    options.OnProvider?.Invoke("armor", publicKey);
                }

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(armorError).Throw();
            }

            if (has && options.OnProvider is not null)
            {
                options.OnProvider(source == "native" ? NativeNames.GetValueOrDefault(NativeStore.Platform, source) : source!,
                    publicKey);
            }

            return ring;
        }

        try
        {
            // providers/index.js: `keys.getSync(publicKey)` for the synchronous variant, `await keys.get(publicKey)` for the async one,
            // whose Armor lookup is the one that reports an access approval through `onStatus`
            IDictionary<string, string> ring = sync ? keys.GetSync(publicKey) : keys.Get(publicKey).GetAwaiter().GetResult();
            if (!sync && !(ring.TryGetValue(publicKey, out string? have) && !string.IsNullOrEmpty(have)) &&
                new BitwardenCustodian().Enabled(options))
            {
                source = "bitwarden";
                ring = new BitwardenCustodian().Get(publicKey);
            }

            return Found(ring);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// `providers(options)` / `providers.sync(options)`: the provider to pass to primitives. Explicitly given providers are
    /// returned as they are.
    /// </summary>
    public static Func<string, IDictionary<string, string>?>? Resolve(KeyAccessOptions options)
    {
        if (options.HasCustomProvider)
        {
            return options.CustomProvider;
        }

        return publicKey => Lookup(publicKey, options, !options.Interactive);
    }

    /// <summary>`require('./armor/index')` of decryptors/index.js (upstream tests stub it).</summary>
    internal static Func<string, DecryptorOptions, IDotenvxLogger?, DecryptorResult> ArmorDecryptor { get; set; } = ArmorProviders.DecryptSrc;

    internal static void ResetToDefaults()
    {
        ArmorDecryptor = ArmorProviders.DecryptSrc;
    }

    /// <summary>`decryptors(options)`: the Armor server-side decryptor, or null when Armor is off.</summary>
    public static Decryptor? ResolveDecryptor(KeyAccessOptions options)
    {
        if (options.HasCustomDecryptor)
        {
            return options.CustomDecryptor;
        }

        if (options.NoArmor)
        {
            return null;
        }

        bool noArmor = string.IsNullOrEmpty(options.Token) && ArmorProviders.SessionFactory(options.Logger).NoArmor();
        if (noArmor)
        {
            return null;
        }

        return (src, decryptOptions) => ArmorDecryptor(src,
            decryptOptions with { OnStatus = options.OnStatus, Token = options.Token, Command = options.Command },
            options.Logger);
    }
}
