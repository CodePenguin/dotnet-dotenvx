
namespace Dotenvx.Providers;

/// <summary>An approval the Armor keyring needs from the user (`onApprovalRequired({ approvalUri, code })`).</summary>
public sealed record ApprovalRequest(string ApprovalUri, string Code);

/// <summary>
/// The options of every provider and of the lookup chain (the `options` object of @dotenvx/providers). `Fk` are the
/// `.env.keys` paths, `Cwd` their base directory, `ProcessEnv` the environment the providers read (the process environment
/// when null).
/// </summary>
public sealed class ProviderOptions
{
    public IReadOnlyDictionary<string, string>? ProcessEnv { get; init; }

    /// <summary>The key file path(s); `.env.keys` when empty.</summary>
    public IReadOnlyList<string>? Fk { get; init; }

    public string? Cwd { get; init; }

    /// <summary>Replaces the OS secret store read (a host with its own store, e.g. a test double); throws like the store does.</summary>
    public Func<string, string?>? NativeGet { get; init; }

    /// <summary>`noNative: true`.</summary>
    public bool NoNative { get; init; }

    /// <summary>`native: false`.</summary>
    public bool NativeOff { get; init; }

    /// <summary>`no1Password: true`.</summary>
    public bool NoOnePassword { get; init; }

    public bool NoBitwarden { get; init; }

    /// <summary>`noArmor: true`.</summary>
    public bool NoArmor { get; init; }

    /// <summary>`armor: false`.</summary>
    public bool ArmorOff { get; init; }

    public string? Hostname { get; init; }
    public string? Token { get; init; }
    public string? DevicePublicKey { get; init; }
    public string? CliVersion { get; init; }

    /// <summary>Runs before each attempted lookup (`onLookup(name)`); it should not throw.</summary>
    public Action<string>? OnLookup { get; init; }

    /// <summary>Runs after each attempted lookup, also when it throws (`onLookupEnd(name)`).</summary>
    public Action<string>? OnLookupEnd { get; init; }

    /// <summary>Receives an unresolved Armor error instead of it being thrown (`onArmorError(error)`).</summary>
    public Action<Exception>? OnArmorError { get; init; }

    /// <summary>Armor access needs approval in a browser (`onApprovalRequired`); not available to the synchronous path.</summary>
    public Action<ApprovalRequest>? OnApprovalRequired { get; init; }

    internal ProviderOptions With(string hostname, string token, string devicePublicKey)
    {
        return new ProviderOptions
        {
            ProcessEnv = ProcessEnv,
            Fk = Fk,
            Cwd = Cwd,
            NoNative = NoNative,
            NativeOff = NativeOff,
            NativeGet = NativeGet,
            NoOnePassword = NoOnePassword,
            NoBitwarden = NoBitwarden,
            NoArmor = NoArmor,
            ArmorOff = ArmorOff,
            Hostname = hostname,
            Token = token,
            DevicePublicKey = devicePublicKey,
            CliVersion = CliVersion,
            OnLookup = OnLookup,
            OnLookupEnd = OnLookupEnd,
            OnArmorError = OnArmorError,
            OnApprovalRequired = OnApprovalRequired
        };
    }

    internal string? Env(string key)
    {
        if (ProcessEnv is not null)
        {
            return ProcessEnv.TryGetValue(key, out string? value) ? value : null;
        }

        return Environment.GetEnvironmentVariable(key);
    }
}

/// <summary>A key lookup: both the asynchronous and the synchronous form return `{ publicKey: privateKey }`, or an empty map.</summary>
public interface IKeyProvider
{
    Task<IDictionary<string, string>> Get(string publicKey);

    IDictionary<string, string> GetSync(string publicKey);
}

/// <summary>An error with a stable code from a provider (`error.code`).</summary>
public class ProviderException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class ProviderErrors
{
    internal static IDictionary<string, string> Empty()
    {
        return new Dictionary<string, string>();
    }

    internal static ApiException? AsApi(Exception error)
    {
        return error as ApiException;
    }
}
