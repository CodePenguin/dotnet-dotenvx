using System.Runtime.ExceptionServices;

namespace Dotenvx.Providers;

/// <remarks>
/// Reference source: @dotenvx/providers index.js `provider(options)`. Tries file, native, Armor, 1Password and Bitwarden in
/// that order and returns the first ring that holds the key. An Armor failure does not stop the chain: it is reported to
/// <see cref="ProviderOptions.OnArmorError" /> (or thrown) after the other providers found nothing.
/// </remarks>
public sealed class ProviderChain(ProviderOptions? options = null) : IKeyProvider
{
    public static readonly IReadOnlyList<string> Order = ["file", "native", "armor", "onepassword", "bitwarden"];

    private readonly ProviderOptions _options = options ?? new ProviderOptions();

    private IKeyProvider Create(string name)
    {
        return name switch
        {
            "file" => new FileProvider(_options),
            "native" => new NativeProvider(_options),
            "armor" => new ArmorProvider(_options),
            "onepassword" => new OnePasswordProvider(_options),
            _ => new BitwardenProvider(_options)
        };
    }

    public async Task<IDictionary<string, string>> Get(string publicKey)
    {
        Exception? armorError = null;
        foreach (string name in Order)
        {
            try
            {
                _options.OnLookup?.Invoke(name);
                IDictionary<string, string> ring = await Create(name).Get(publicKey);
                if (Found(ring, publicKey))
                {
                    return ring;
                }
            }
            catch (Exception error) when (name == "armor")
            {
                armorError = error;
            }
            finally
            {
                _options.OnLookupEnd?.Invoke(name);
            }
        }

        return Finish(armorError);
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        Exception? armorError = null;
        foreach (string name in Order)
        {
            try
            {
                _options.OnLookup?.Invoke(name);
                IDictionary<string, string> ring = Create(name).GetSync(publicKey);
                if (Found(ring, publicKey))
                {
                    return ring;
                }
            }
            catch (Exception error) when (name == "armor")
            {
                armorError = error;
            }
            finally
            {
                _options.OnLookupEnd?.Invoke(name);
            }
        }

        return Finish(armorError);
    }

    private static bool Found(IDictionary<string, string> ring, string publicKey)
    {
        return ring.TryGetValue(publicKey, out string? value) && !string.IsNullOrEmpty(value);
    }

    private IDictionary<string, string> Finish(Exception? armorError)
    {
        if (armorError is null)
        {
            return ProviderErrors.Empty();
        }

        // CLI consumers may have an interactive fallback after this chain.
        if (_options.OnArmorError is null)
        {
            ExceptionDispatchInfo.Capture(armorError).Throw();
        }

        _options.OnArmorError!(armorError);

        return ProviderErrors.Empty();
    }
}
