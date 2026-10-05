namespace Dotenvx.Interop.DotenvxJs.Helpers;

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

    /// <summary>
    /// `providers(options)`: the provider to pass to primitives, or null when none applies (the caller then passes no
    /// provider at all, like envs.js' `options.provider = null`).
    /// </summary>
    public static Func<string, IDictionary<string, string>?>? Resolve(bool noNative)
    {
        return UseNative(noNative) ? NativeProvider : null;
    }
}
