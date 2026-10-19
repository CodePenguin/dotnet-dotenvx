using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/local/bitwarden.js. The private key lives in a Bitwarden personal vault item;
/// only a nonsecret locator (base64 of `{ item, userId, serverUrl }`) is written to the dotenvx settings, under
/// `DOTENVX_BITWARDEN_&lt;public key&gt;`. A locked vault is unlocked with the master password on an interactive terminal;
/// the unlocked session is kept for the rest of the process.
/// </remarks>
public sealed partial class BitwardenCustodian : ILocalCustodian, ISyncCustodian
{
    private const string Prefix = "DOTENVX_BITWARDEN_";
    private const string PasswordEnv = "DOTENVX_BITWARDEN_PASSWORD";

    private static string? _unlockedSession;

    [GeneratedRegex("^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();

    public string Id => "bitwarden";
    public string Name => "Bitwarden";
    public string Custody => "local";

    private static ProviderException Failure(string message)
    {
        return new ProviderException("BITWARDEN_FAILED", message);
    }

    private static ProviderException CommandFailure()
    {
        return Failure("Bitwarden CLI operation failed; check sign-in and unlock your vault with BW_SESSION");
    }

    /// <summary>`commandEnv()`: the process environment plus the session unlocked by this process, if any.</summary>
    public static IReadOnlyDictionary<string, string> CommandEnv()
    {
        Dictionary<string, string> env = Helpers.Helpers.CurrentProcessEnv().Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
        if (_unlockedSession is not null)
        {
            env["BW_SESSION"] = _unlockedSession;
        }

        return env;
    }

    private static string Run(string[] args, string? input = null, IReadOnlyDictionary<string, string>? env = null)
    {
        // only the additions to the process environment are passed; the session is one of them
        IReadOnlyDictionary<string, string>? additions = env ??
                                                        (_unlockedSession is null
                                                            ? null
                                                            : new Dictionary<string, string> { ["BW_SESSION"] = _unlockedSession });
        SecretCliResult result = SecretCli.Run("bw", [.. args, "--nointeraction"], additions, input);
        if (result.NotFound || result.ExitCode != 0)
        {
            throw CommandFailure();
        }

        return result.Stdout;
    }

    private static JsonNode? Parse(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            throw Failure("Bitwarden CLI returned an invalid response");
        }
    }

    private static string? Str(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    private static bool IsId(string? value)
    {
        return value is not null && IdRegex().IsMatch(value);
    }

    public bool Enabled(KeyAccessOptions? options = null)
    {
        return options?.NoBitwarden != true && Environment.GetEnvironmentVariable("DOTENVX_NO_BITWARDEN") != "true";
    }

    public bool Available()
    {
        try
        {
            return Regex.IsMatch(Run(["--version"]).Trim(), @"^\d+\.\d+\.\d+");
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Configured(KeyAccessOptions? options = null)
    {
        ConfStore? store = new Session().OpenStore();

        return store is not null && store.Keys.Any(key => key.StartsWith(Prefix, StringComparison.Ordinal));
    }

    private sealed record BitwardenLocation(string Item, string UserId, string ServerUrl);

    private static BitwardenLocation? Location(string publicKey)
    {
        string? value = new Session().OpenStore()?.GetString($"{Prefix}{publicKey}");
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Base64 keeps the nonsecret JSON locator safe in the dotenv settings file.
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            decoded = "";
        }

        JsonNode? location = Parse(decoded);
        if (location is not JsonObject obj || !IsId(Str(obj["item"])) || !IsId(Str(obj["userId"])) ||
            Str(obj["serverUrl"]) is null)
        {
            throw Failure("invalid Bitwarden private-key reference in dotenvx settings");
        }

        return new BitwardenLocation(Str(obj["item"])!, Str(obj["userId"])!, Str(obj["serverUrl"])!);
    }

    private static JsonNode CheckIdentity(JsonNode? status, BitwardenLocation? location)
    {
        if (status is not JsonObject obj || Str(obj["status"]) != "unlocked" || !IsId(Str(obj["userId"])))
        {
            throw CommandFailure();
        }

        if (location is not null && (Str(obj["userId"]) != location.UserId ||
                                     (Str(obj["serverUrl"]) ?? "") != location.ServerUrl))
        {
            throw Failure("Bitwarden account or server does not match the stored private-key reference");
        }

        return obj;
    }

    private static JsonNode Authenticate(BitwardenLocation? location)
    {
        JsonNode? status = Parse(Run(["status"]));
        if (location is not null && status is JsonObject known && !string.IsNullOrEmpty(Str(known["userId"])) &&
            (Str(known["userId"]) != location.UserId || (Str(known["serverUrl"]) ?? "") != location.ServerUrl))
        {
            throw Failure("Bitwarden account or server does not match the stored private-key reference");
        }

        if (status is JsonObject current && Str(current["status"]) == "unlocked")
        {
            return CheckIdentity(status, location);
        }

        _unlockedSession = null;
        string? state = status is JsonObject locked ? Str(locked["status"]) : null;
        if (state == "unauthenticated")
        {
            throw Failure("Sign in to Bitwarden once with bw login, then retry; dotenvx will prompt to unlock your vault");
        }

        if (state != "locked")
        {
            throw CommandFailure();
        }

        if (!SecretCli.IsInteractive() || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            throw Failure("Bitwarden is locked; set an unlocked BW_SESSION for noninteractive use");
        }

        string password;
        Spinners.Pause();
        try
        {
            password = Prompts.Password("Bitwarden master password", "◇", "=") ?? throw new PromptCancelledException();
        }
        finally
        {
            Spinners.Resume();
        }

        Dictionary<string, string> passwordEnv = new(CommandEnvWithoutSession()) { [PasswordEnv] = password };
        string session = Run(["unlock", "--passwordenv", PasswordEnv, "--raw"], null, passwordEnv).Trim();
        if (session.Length == 0)
        {
            throw CommandFailure();
        }

        _unlockedSession = session;
        try
        {
            return CheckIdentity(Parse(Run(["status"])), location);
        }
        catch (Exception)
        {
            _unlockedSession = null;

            throw;
        }
    }

    private static IReadOnlyDictionary<string, string> CommandEnvWithoutSession()
    {
        return Helpers.Helpers.CurrentProcessEnv().Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
    }

    private static IDictionary<string, string> Verified(string publicKey, string privateKey)
    {
        if (!MatchesStoredKey.Matches(publicKey, privateKey))
        {
            throw Failure("Bitwarden private key does not match the .env public key");
        }

        return new Dictionary<string, string> { [publicKey] = privateKey };
    }

    /// <summary>The synchronous read of the providers (needs an unlocked BW_SESSION; never prompts).</summary>
    public IDictionary<string, string> GetSync(string publicKey)
    {
        return new BitwardenProvider(new ProviderOptions { ProcessEnv = CommandEnv() }).GetSync(publicKey);
    }

    /// <summary>`get(publicKey)`: authenticates (unlocking the vault interactively when needed), then reads the key.</summary>
    public IDictionary<string, string> Get(string publicKey)
    {
        BitwardenLocation? location = Location(publicKey);
        if (location is null)
        {
            return new Dictionary<string, string>();
        }

        Authenticate(location);

        return new BitwardenProvider(new ProviderOptions { ProcessEnv = CommandEnv() }).GetSync(publicKey);
    }

    public StoreResult Store(string publicKey, string privateKey, StoreContext context)
    {
        Set(publicKey, privateKey);

        return new StoreResult();
    }

    public void Set(string publicKey, string privateKey)
    {
        Verified(publicKey, privateKey);
        if (Location(publicKey) is not null)
        {
            Get(publicKey);

            return;
        }

        JsonNode status = Authenticate(null);
        JsonObject template = new()
        {
            ["organizationId"] = null,
            ["collectionIds"] = new JsonArray(),
            ["folderId"] = null,
            ["type"] = 1,
            ["name"] = $"dotenvx ({ArmoredKeyDisplay.Display(publicKey)})",
            ["notes"] = null,
            ["favorite"] = false,
            ["fields"] = new JsonArray(new JsonObject { ["name"] = "public_key", ["value"] = publicKey, ["type"] = 0 }),
            ["login"] = new JsonObject
            {
                ["username"] = "private_key", ["password"] = privateKey, ["totp"] = null, ["uris"] = new JsonArray()
            }
        };
        // Pass encoded JSON on stdin, never secrets in command arguments or files.
        JsonNode? item = Parse(Run(["create", "item"], Convert.ToBase64String(Encoding.UTF8.GetBytes(ArmorWire.Stringify(template)))));
        if (item is not JsonObject created || !IsId(Str(created["id"])) || !string.IsNullOrEmpty(Str(created["organizationId"])))
        {
            throw Failure("Bitwarden did not return a personal vault item");
        }

        string itemId = Str(created["id"])!;
        string saved = Run(["get", "password", itemId]).Trim();
        Verified(publicKey, saved);
        if (saved != privateKey)
        {
            throw Failure("could not verify private key in Bitwarden");
        }

        JsonObject location = new()
        {
            ["item"] = itemId, ["userId"] = Str(status["userId"]), ["serverUrl"] = Str(status["serverUrl"]) ?? ""
        };
        new Session().CreateStore().Set($"{Prefix}{publicKey}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(ArmorWire.Stringify(location))));
    }

    public void Delete(string publicKey)
    {
        BitwardenLocation? location = Location(publicKey);
        if (location is null)
        {
            return;
        }

        Authenticate(location);
        Run(["delete", "item", location.Item]);
        new Session().OpenStore()?.Delete($"{Prefix}{publicKey}");
    }
}
