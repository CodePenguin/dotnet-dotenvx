using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>The inputs a custodian needs to store a new private key (the `context` of custodians/index.js).</summary>
public sealed class StoreContext
{
    public string? KeysSrc { get; set; }
    public string PrivateKeyName { get; set; } = "";
    public string Comment { get; set; } = "";
    public string? KeysFilepath { get; set; }
    public string? Token { get; set; }

    /// <summary>The prompt may offer "back" to return to the custody menu.</summary>
    public bool AllowCustodyBack { get; set; }
}

/// <summary>
/// What storing a private key changed: the new `.env.keys` text (file custody), a key added to the OS secret store, or a
/// request to use another custodian when the store is unavailable.
/// </summary>
public sealed class StoreResult
{
    public bool HasKeysSrc { get; init; }
    public string? KeysSrc { get; init; }
    public bool NativePrivateKeyAdded { get; init; }
    public string? Fallback { get; init; }
}

/// <summary>A place private keys live (custodians/index.js): local (file, OS store, password managers) or managed (Armor).</summary>
public interface ICustodian
{
    string Id { get; }
    string Name { get; }

    /// <summary>"local" or "managed".</summary>
    string Custody { get; }

    bool Enabled(KeyAccessOptions? options = null);

    bool Available();

    /// <summary>Whether the user has set this custodian up (a sign-in, a stored reference); null for custodians without the notion.</summary>
    bool Configured(KeyAccessOptions? options = null);

    IDictionary<string, string> Get(string publicKey);

    StoreResult? Store(string publicKey, string privateKey, StoreContext context);

    /// <summary>Whether the custodian has a read at all (`custodian.get`); the file custodian has none, its keys are resolved with the env files.</summary>
    bool CanGet => true;
}

/// <summary>A custodian that can also read and remove the keys it holds (everything but Armor).</summary>
public interface ILocalCustodian : ICustodian
{
    void Set(string publicKey, string privateKey);

    void Delete(string publicKey);
}

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/index.js. An explicit registration of the built-in custodians:
/// native, 1Password, Bitwarden, file, Armor.
/// </remarks>
public static class Custodians
{
    /// <summary>`module.exports = createRegistry(builtins)`.</summary>
    public static CustodianRegistry Registry { get; } = new(
    [
        new NativeCustodian(), new OnePasswordCustodian(), new BitwardenCustodian(), new FileCustodian(), new ArmorCustodian()
    ]);

    /// <summary>`createRegistry(custodians)`.</summary>
    public static CustodianRegistry CreateRegistry(IEnumerable<ICustodian> custodians)
    {
        return new CustodianRegistry(custodians);
    }

    public static ICustodian Get(string id)
    {
        return Registry.Get(id);
    }

    /// <summary>`store(selection, ...)`: stores the key with the chosen custodian, following a requested fallback.</summary>
    public static StoreResult Store(string id, string publicKey, string privateKey, StoreContext context)
    {
        return Registry.Store(id, publicKey, privateKey, context);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/custodians/local/file.js. File keys are staged for the caller to commit with the encrypted env files.</remarks>
public sealed class FileCustodian : ICustodian
{
    public bool CanGet => false;

    public string Id => "file";
    public string Name => "File (.env.keys)";
    public string Custody => "local";

    public bool Enabled(KeyAccessOptions? options = null)
    {
        return true;
    }

    public bool Available()
    {
        return true;
    }

    public bool Configured(KeyAccessOptions? options = null)
    {
        return true;
    }

    public IDictionary<string, string> Get(string publicKey)
    {
        return new Dictionary<string, string>();
    }

    public StoreResult Store(string publicKey, string privateKey, StoreContext context)
    {
        return new StoreResult
        {
            HasKeysSrc = true,
            KeysSrc = Helpers.Helpers.MutateKeysSrc(context.KeysSrc, context.PrivateKeyName, privateKey, context.Comment)
        };
    }
}

/// <remarks>Reference source: dotenvx/src/lib/custodians/local/native/{index,store}.js.</remarks>
public sealed class NativeCustodian : ILocalCustodian, ISyncCustodian
{
    public string Id => "native";

    public string Name
    {
        get
        {
            string? store = NativeStore.Platform switch
            {
                "darwin" => "macOS Keychain",
                "win32" => "Windows Credential Manager",
                "linux" => "Linux Secret Service",
                _ => null
            };

            return $"OS{(store is null ? "" : $" ({store})")}";
        }
    }

    public string Custody => "local";

    public bool Enabled(KeyAccessOptions? options = null)
    {
        return NativeStore.IsSupportedPlatform && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) &&
               options?.NoNative != true && Environment.GetEnvironmentVariable("DOTENVX_NO_NATIVE") != "true";
    }

    public bool Available()
    {
        return true;
    }

    public bool Configured(KeyAccessOptions? options = null)
    {
        return true;
    }

    public IDictionary<string, string> Get(string publicKey)
    {
        string? value = NativeStore.Get(publicKey);

        return string.IsNullOrEmpty(value)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [publicKey] = value };
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        return Get(publicKey);
    }

    public void Set(string publicKey, string privateKey)
    {
        NativeStore.Set(publicKey, privateKey, publicKey);
    }

    public void Delete(string publicKey)
    {
        NativeStore.Delete(publicKey);
    }

    public StoreResult Store(string publicKey, string privateKey, StoreContext context)
    {
        return StorePrivateKey(publicKey, privateKey, context.KeysFilepath)
            ? new StoreResult { NativePrivateKeyAdded = true }
            : new StoreResult { Fallback = "file" };
    }

    /// <summary>store.js: false when the OS store is unavailable (the caller falls back to the file); a write that cannot be read back throws.</summary>
    private static bool StorePrivateKey(string publicKey, string privateKey, string? keysFilepath)
    {
        try
        {
            NativeStore.Set(publicKey, privateKey, $"dotenvx ({ArmoredKeyDisplay.Display(publicKey)})");
        }
        catch (Providers.NativeStoreException error) when (error.Code == "NATIVE_UNAVAILABLE")
        {
            Logger?.Warn($"OS secret store unavailable; saving private key to {keysFilepath}");

            return false;
        }

        // A successful write must be readable before committing the encrypted file.
        // Read failures must never fall back to plaintext storage.
        if (NativeStore.Get(publicKey) != privateKey)
        {
            throw new Providers.NativeStoreException("NATIVE_VERIFY_FAILED", "could not verify private key in OS secret store");
        }

        return true;
    }

    /// <summary>The logger the unavailable-store warning goes to (set by the CLI).</summary>
    public static Shared.IDotenvxLogger? Logger { get; set; }
}
