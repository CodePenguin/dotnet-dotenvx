using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Dotenvx.Providers;

/// <summary>`matchesStoredKey(publicKey, value)`: the stored value derives the public key asked for.</summary>
internal static class MatchesStoredKey
{
    public static bool Matches(string publicKey, string value)
    {
        try
        {
            return Primitives.Primitives.Derive(value) == publicKey;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <remarks>
/// Reference source: @dotenvx/providers passwordCommand.js. Runs a password-manager CLI (`op`, `bw`) with the providers'
/// environment, a two-minute timeout and no stdin. The error never carries the program's output, which may hold secrets.
/// </remarks>
internal sealed class PasswordCommand(string binary, ProviderOptions options, Func<ProviderException> failure)
{
    public string GetSync(IReadOnlyList<string> args)
    {
        ProcessOutcome outcome = ProcessRunner.Run(new ProcessRequest(binary, args, null, 120000, options.ProcessEnv, true));
        if (!outcome.Succeeded)
        {
            throw failure();
        }

        return outcome.Stdout.Trim();
    }

    public Task<string> Get(IReadOnlyList<string> args)
    {
        return Task.Run(() => GetSync(args));
    }
}

/// <remarks>
/// Reference source: @dotenvx/providers onepassword.js. The reference of a stored key (`DOTENVX_ONEPASSWORD_&lt;public key&gt;`
/// in the dotenvx settings) is `account|op://vault/item/(private_key|password)`; the value read with `op read` must derive the
/// public key.
/// </remarks>
public sealed class OnePasswordProvider(ProviderOptions options) : IKeyProvider
{
    private static readonly Regex Account = new("^[a-z0-9]{26}\\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Reference = new("^op://[a-z0-9]{26}/[a-z0-9]{26}/(?:private_key|password)\\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static ProviderException Failure(string? message = null)
    {
        return new ProviderException("1PASSWORD_FAILED",
            message ?? "1Password CLI could not read the private key; check sign-in and vault permissions");
    }

    private static List<string>? Location(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string[] parts = value.Split('|');
        if (parts.Length != 2 || !Account.IsMatch(parts[0]) || !Reference.IsMatch(parts[1]))
        {
            throw Failure("invalid 1Password private-key reference in dotenvx settings");
        }

        return ["read", parts[1], "--no-newline", $"--account={parts[0]}"];
    }

    private static IDictionary<string, string> Verified(string publicKey, string value)
    {
        if (MatchesStoredKey.Matches(publicKey, value))
        {
            return new Dictionary<string, string> { [publicKey] = value };
        }

        throw Failure("1Password private key does not match the .env public key");
    }

    private bool Disabled => options.NoOnePassword || options.Env("DOTENVX_NO_1PASSWORD") == "true";

    public async Task<IDictionary<string, string>> Get(string publicKey)
    {
        if (Disabled)
        {
            return ProviderErrors.Empty();
        }

        List<string>? args = Location(await ProviderSettings.Get($"DOTENVX_ONEPASSWORD_{publicKey}", options));
        if (args is null)
        {
            return ProviderErrors.Empty();
        }

        return Verified(publicKey, await new PasswordCommand("op", options, () => Failure()).Get(args));
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        if (Disabled)
        {
            return ProviderErrors.Empty();
        }

        List<string>? args = Location(ProviderSettings.GetSync($"DOTENVX_ONEPASSWORD_{publicKey}", options));
        if (args is null)
        {
            return ProviderErrors.Empty();
        }

        return Verified(publicKey, new PasswordCommand("op", options, () => Failure()).GetSync(args));
    }
}

/// <remarks>
/// Reference source: @dotenvx/providers bitwarden.js. The reference of a stored key (`DOTENVX_BITWARDEN_&lt;public key&gt;`) is the
/// base64 of `{ item, userId, serverUrl }`; `bw status` must show an unlocked vault of that user and server, then
/// `bw get password &lt;item&gt;` must derive the public key. Needs an unlocked `BW_SESSION`.
/// </remarks>
public sealed class BitwardenProvider(ProviderOptions options) : IKeyProvider
{
    private static readonly Regex Id = new("^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}\\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static ProviderException Failure(string? message = null)
    {
        return new ProviderException("BITWARDEN_FAILED",
            message ?? "Bitwarden CLI operation failed; check sign-in and unlock your vault with BW_SESSION");
    }

    private static JsonNode? Parse(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (Exception)
        {
            throw Failure("Bitwarden CLI returned an invalid response");
        }
    }

    private sealed record BitwardenLocation(string Item, string UserId, string ServerUrl);

    private static string? Str(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    private static bool IsId(JsonNode? node)
    {
        string? text = Str(node);

        return text is not null && Id.IsMatch(text);
    }

    private static BitwardenLocation? Location(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        JsonNode? location;
        try
        {
            location = Parse(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
        }
        catch (FormatException)
        {
            throw Failure("invalid Bitwarden private-key reference in dotenvx settings");
        }

        if (location is not JsonObject obj || !IsId(obj["item"]) || !IsId(obj["userId"]) || Str(obj["serverUrl"]) is null)
        {
            throw Failure("invalid Bitwarden private-key reference in dotenvx settings");
        }

        return new BitwardenLocation(Str(obj["item"])!, Str(obj["userId"])!, Str(obj["serverUrl"])!);
    }

    private static void CheckIdentity(string value, BitwardenLocation location)
    {
        if (Parse(value) is not JsonObject obj || Str(obj["status"]) != "unlocked" || !IsId(obj["userId"]))
        {
            throw Failure();
        }

        if (Str(obj["userId"]) != location.UserId || (Str(obj["serverUrl"]) ?? "") != location.ServerUrl)
        {
            throw Failure("Bitwarden account or server does not match the stored private-key reference");
        }
    }

    private static IDictionary<string, string> Verified(string publicKey, string value)
    {
        if (MatchesStoredKey.Matches(publicKey, value))
        {
            return new Dictionary<string, string> { [publicKey] = value };
        }

        throw Failure("Bitwarden private key does not match the .env public key");
    }

    private bool Disabled => options.NoBitwarden || options.Env("DOTENVX_NO_BITWARDEN") == "true";

    private PasswordCommand Cli()
    {
        if (string.IsNullOrEmpty(options.Env("BW_SESSION")))
        {
            throw Failure("Bitwarden requires an unlocked BW_SESSION; run bw login, then bw unlock --raw and set BW_SESSION");
        }

        return new PasswordCommand("bw", options, () => Failure());
    }

    public async Task<IDictionary<string, string>> Get(string publicKey)
    {
        if (Disabled)
        {
            return ProviderErrors.Empty();
        }

        BitwardenLocation? location = Location(await ProviderSettings.Get($"DOTENVX_BITWARDEN_{publicKey}", options));
        if (location is null)
        {
            return ProviderErrors.Empty();
        }

        PasswordCommand cli = Cli();
        CheckIdentity(await cli.Get(["status", "--nointeraction"]), location);

        return Verified(publicKey, await cli.Get(["get", "password", location.Item, "--nointeraction"]));
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        if (Disabled)
        {
            return ProviderErrors.Empty();
        }

        BitwardenLocation? location = Location(ProviderSettings.GetSync($"DOTENVX_BITWARDEN_{publicKey}", options));
        if (location is null)
        {
            return ProviderErrors.Empty();
        }

        PasswordCommand cli = Cli();
        CheckIdentity(cli.GetSync(["status", "--nointeraction"]), location);

        return Verified(publicKey, cli.GetSync(["get", "password", location.Item, "--nointeraction"]));
    }
}
