using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>One entry of the custody menu (`{ name, value, disabled }`).</summary>
public sealed record CustodianChoice(string Name, string Value, bool Disabled);

/// <summary>A custodian whose keys can also be read synchronously (`getSync`); the others fail an ordered synchronous lookup.</summary>
public interface ISyncCustodian : ICustodian
{
    IDictionary<string, string> GetSync(string publicKey);
}

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/index.js `createRegistry(custodians)`. The registry of the built-in custodians is
/// <see cref="Custodians.Registry" />; a registry can also be made from any set of <see cref="ICustodian" /> instances (custom
/// custodians). Upstream's duck-typed checks ("custodian x requires enabled()") are the interface here, so only the id is validated.
/// </remarks>
public sealed class CustodianRegistry
{
    private readonly Dictionary<string, ICustodian> _entries = [];

    public CustodianRegistry(IEnumerable<ICustodian> custodians)
    {
        foreach (ICustodian? custodian in custodians)
        {
            if (custodian is null || string.IsNullOrEmpty(custodian.Id) || _entries.ContainsKey(custodian.Id))
            {
                throw new Exception("custodians must have unique, nonempty ids");
            }

            _entries[custodian.Id] = custodian;
        }
    }

    public ICustodian Get(string id)
    {
        return _entries.TryGetValue(id, out ICustodian? custodian) ? custodian : throw new Exception($"unknown custodian: {id}");
    }

    /// <summary>`choices(options, custody)`: the custodians of one custody, disabled when off or unavailable (never probed when off).</summary>
    public IReadOnlyList<CustodianChoice> Choices(KeyAccessOptions? options = null, string custody = "local")
    {
        List<CustodianChoice> choices = [];
        foreach (ICustodian custodian in _entries.Values)
        {
            if ((string.IsNullOrEmpty(custodian.Custody) ? "local" : custodian.Custody) != custody)
            {
                continue;
            }

            choices.Add(new CustodianChoice(custodian.Name, custodian.Id, !custodian.Enabled(options) || !custodian.Available()));
        }

        return choices;
    }

    /// <summary>
    /// `providers(options, sync)`: one lookup function per enabled, configured local custodian that can be read, in registration order.
    /// Disabled custodians are never read; a custodian without a synchronous read fails a synchronous request.
    /// </summary>
    public IReadOnlyList<Func<string, IDictionary<string, string>>> Providers(KeyAccessOptions? options = null, bool sync = false)
    {
        List<Func<string, IDictionary<string, string>>> providers = [];
        foreach (ICustodian custodian in _entries.Values)
        {
            if (custodian.Custody == "managed")
            {
                continue;
            }

            if (!custodian.Enabled(options) || !custodian.CanGet || !custodian.Configured(options))
            {
                continue;
            }

            if (sync && custodian is not ISyncCustodian)
            {
                throw new Exception($"custodian {custodian.Id} does not support synchronous reads");
            }

            ICustodian current = custodian;
            IDictionary<string, string> Found(string publicKey, IDictionary<string, string> ring)
            {
                if (ring.TryGetValue(publicKey, out string? value) && !string.IsNullOrEmpty(value) && options?.OnProvider is not null)
                {
                    options.OnProvider(current.Id == "native" ? KeyProviders.NativeNames.GetValueOrDefault(NativeStore.Platform, current.Id) : current.Id,
                        publicKey);
                }

                return ring;
            }

            providers.Add(sync
                ? publicKey => Found(publicKey, ((ISyncCustodian)current).GetSync(publicKey))
                : publicKey => Found(publicKey, current.Get(publicKey)));
        }

        return providers;
    }

    /// <summary>`store(selection, ...)`: stores the key with the chosen custodian, following a requested fallback.</summary>
    public StoreResult Store(string id, string publicKey, string privateKey, StoreContext? context = null)
    {
        context ??= new StoreContext();
        StoreResult result = Get(id).Store(publicKey, privateKey, context) ?? new StoreResult();
        // Only the native custodian's unavailable-write path requests fallback.
        // Authentication and verification errors propagate without writing a file.
        return result.Fallback is not null ? Get(result.Fallback).Store(publicKey, privateKey, context) ?? new StoreResult() : result;
    }
}
