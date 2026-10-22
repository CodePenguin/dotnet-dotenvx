using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/local/onepassword.js. The private key lives in a 1Password item; only a
/// nonsecret locator (`account|op://vault/item/password`) is written to the dotenvx settings, under
/// `DOTENVX_ONEPASSWORD_&lt;public key&gt;`.
/// </remarks>
internal sealed partial class OnePasswordCustodian : ILocalCustodian, ISyncCustodian
{
    private const string Prefix = "DOTENVX_ONEPASSWORD_";

    [GeneratedRegex("^[a-z0-9]{26}$", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();

    [GeneratedRegex("^op://[a-z0-9]{26}/[a-z0-9]{26}/(?:private_key|password)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex("account is not signed in", RegexOptions.IgnoreCase)]
    private static partial Regex NotSignedInRegex();

    public string Id => "onepassword";
    public string Name => "1Password";
    public string Custody => "local";

    /// <summary>The reason of a failure the caller may recover from (`error.reason`).</summary>
    private sealed class OnePasswordException(string message, string? reason = null)
        : ProviderException("1PASSWORD_FAILED", message)
    {
        public string? Reason { get; } = reason;
    }

    private static OnePasswordException Failure(string message)
    {
        return new OnePasswordException(message);
    }

    private static OnePasswordException CommandError(string[] args, string stderr)
    {
        string step = args[0] == "item"
            ? args[1] == "delete" ? "delete the key item" : "create the key item"
            : args[0] switch
            {
                "whoami" => "check the signed-in account",
                "signin" => "sign in",
                "read" => "read the private key",
                _ => "check availability"
            };
        if (NotSignedInRegex().IsMatch(stderr))
        {
            return new OnePasswordException(
                $"1Password could not {step}: account is not signed in. Sign in with op signin or enable the desktop app CLI integration",
                "NOT_SIGNED_IN");
        }

        return Failure($"1Password could not {step}; check sign-in and vault permissions");
    }

    private static string Run(string[] args, string? input = null)
    {
        // On Unix, Node supplies a socket for stdin. op only detects piped JSON through a real pipe, so cat bridges the
        // socket without writing a file.
        bool pipeInput = input is not null && !OperatingSystem.IsWindows();
        SecretCliResult result = pipeInput
            ? SecretCli.Run("/bin/sh", ["-c", "cat | op \"$@\"", "dotenvx-op", .. args], null, input)
            : SecretCli.Run("op", args, null, input);
        // Subprocess errors may include secrets from stdin or stdout.
        if (result.NotFound || result.ExitCode != 0)
        {
            throw CommandError(args, result.Stderr);
        }

        return result.Stdout;
    }

    private static JsonNode ParseResponse(string value)
    {
        try
        {
            return JsonNode.Parse(value) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Failure("1Password CLI returned an invalid response");
        }
    }

    public bool Enabled(KeyAccessOptions? options = null)
    {
        return options?.No1Password != true && Environment.GetEnvironmentVariable("DOTENVX_NO_1PASSWORD") != "true";
    }

    public bool Available()
    {
        try
        {
            // Availability means the CLI is installed. Account setup and authentication are checked by Store only after the
            // user selects 1Password.
            return Run(["--version"]).Trim().StartsWith("2.", StringComparison.Ordinal);
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

    private static (string Account, string Reference)? Location(string publicKey)
    {
        string? value = new Session().OpenStore()?.GetString($"{Prefix}{publicKey}");
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string[] parts = value.Split('|');
        string account = parts[0];
        string reference = parts.Length > 1 ? parts[1] : "";
        if (!IdRegex().IsMatch(account) || !ReferenceRegex().IsMatch(reference))
        {
            throw Failure("invalid 1Password private-key reference in dotenvx settings");
        }

        return (account, reference);
    }

    public IDictionary<string, string> Get(string publicKey)
    {
        return new OnePasswordProvider(new ProviderOptions()).GetSync(publicKey);
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        return Get(publicKey);
    }

    public StoreResult Store(string publicKey, string privateKey, StoreContext context)
    {
        Set(publicKey, privateKey);

        return new StoreResult();
    }

    public void Set(string publicKey, string privateKey)
    {
        JsonNode identity;
        try
        {
            identity = ParseResponse(Run(["whoami", "--format=json"]));
        }
        catch (OnePasswordException error) when (error.Reason == "NOT_SIGNED_IN" &&
                                                 string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OP_SERVICE_ACCOUNT_TOKEN")))
        {
            // whoami checks authentication but does not initiate the desktop app flow.
            // Discard signin output; never print or evaluate a returned session token.
            Run(["signin"]);
            identity = ParseResponse(Run(["whoami", "--format=json"]));
        }

        string account = identity["account_uuid"]?.ToString() ?? "";
        if (!IdRegex().IsMatch(account))
        {
            throw Failure("could not identify the signed-in 1Password account");
        }

        string title = $"dotenvx ({ArmoredKeyDisplay.Display(publicKey)})";
        JsonObject template = new()
        {
            ["title"] = title,
            ["category"] = "PASSWORD",
            ["tags"] = new JsonArray("dotenvx"),
            ["fields"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "password",
                    ["label"] = "private_key",
                    ["type"] = "CONCEALED",
                    ["purpose"] = "PASSWORD",
                    ["value"] = privateKey
                },
                new JsonObject
                {
                    ["id"] = "public_key",
                    ["label"] = "public_key",
                    ["type"] = "STRING",
                    ["value"] = publicKey
                })
        };
        JsonNode item = ParseResponse(Run(["item", "create", "-", "--format=json", $"--account={account}"],
            ArmorWire.Stringify(template)));
        string itemId = item["id"]?.ToString() ?? "";
        if (!IdRegex().IsMatch(itemId))
        {
            throw Failure("1Password did not return a saved item ID");
        }

        string vault = item["vault"]?["id"]?.ToString() ?? "";
        if (!IdRegex().IsMatch(vault))
        {
            throw Failure("1Password did not return the saved item vault ID");
        }

        string reference = $"op://{vault}/{itemId}/password";
        string saved = Run(["read", reference, "--no-newline", $"--account={account}"]);
        if (saved.Trim() != privateKey)
        {
            throw Failure("could not verify private key in 1Password");
        }

        // Only a nonsecret locator is persisted locally, never the private key.
        new Session().CreateStore().Set($"{Prefix}{publicKey}", $"{account}|{reference}");
    }

    public void Delete(string publicKey)
    {
        (string Account, string Reference)? location = Location(publicKey);
        if (location is null)
        {
            return;
        }

        string[] segments = location.Value.Reference["op://".Length..].Split('/');
        Run(["item", "delete", segments[1], $"--vault={segments[0]}", $"--account={location.Value.Account}"]);
        new Session().OpenStore()?.Delete($"{Prefix}{publicKey}");
    }
}
