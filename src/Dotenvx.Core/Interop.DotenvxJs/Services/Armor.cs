using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Interop.DotenvxJs.Helpers.EnvKeyFiles;
using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <summary>What the `armor` services return: the API's own JSON plus the key names (`{ ...json, privateKeyName, ... }`).</summary>
public record ArmorResult(
    bool Changed, string PrivateKeyName, string? PrivateKeyValue, string? PublicKeyValue, string? Team = null);

/// <summary>Shared team-selection flow of armorUp/Down/Pull/Push (`isTeamRequiredError` + `teamChoicesFromMeta`).</summary>
internal static class ArmorTeam
{
    public static bool IsTeamRequiredError(Exception? error)
    {
        return error is ApiException { Code: "TEAM_REQUIRED" or "DOTENVX_TEAM_REQUIRED" };
    }

    public static Prompts.Choice[] ChoicesFromMeta(JsonNode? meta)
    {
        // teamChoicesFromMeta.js: `meta.organizations.map(...)` throws a TypeError when meta has none
        JsonArray organizations = meta?["organizations"]?.AsArray() ??
                                  throw new InvalidOperationException(
                                      "Cannot read properties of undefined (reading 'map')");

        return
        [
            .. organizations.Select(org =>
                new Prompts.Choice(org!["provider_slug"]!.ToString(), org["provider_slug"]!.ToString()))
        ];
    }

    /// <summary>Calls <paramref name="call" /> with the team; when none is given retries with the chosen team on a team-required error.</summary>
    public static JsonNode Run(string? team, Func<string?, JsonNode> call)
    {
        if (!string.IsNullOrEmpty(team))
        {
            return call(team);
        }

        try
        {
            return call(null);
        }
        catch (Exception error) when (IsTeamRequiredError(error))
        {
            Prompts.Choice[] choices = ChoicesFromMeta(((ApiException)error).Meta);

            if (choices.Length == 0)
            {
                throw;
            }

            string chosen = choices[0].Value;
            if (choices.Length > 1)
            {
                chosen = Prompts.Select(new Prompts.SelectRequest("Select team", choices) { Prefix = "⛨" }) ?? throw new PromptCancelledException();
            }

            return call(chosen);
        }
    }

    public static string? AsString(JsonNode? node)
    {
        return node is null ? null : node.ToString();
    }

    public static bool AsBool(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue(out bool boolean) && boolean;
    }
}


/// <remarks>Reference source: dotenvx/src/lib/services/armorUp.js</remarks>
public class ArmorUp(string hostname, string? token, string devicePublicKey, string? envFile = null, string? team = null)
{
    private static readonly Func<string, string?, string, string?, string?, ArmorUp> DefaultFactory =
        (hostname, token, devicePublicKey, envFile, team) => new ArmorUp(hostname, token, devicePublicKey, envFile, team);

    public static Func<string, string?, string, string?, string?, ArmorUp> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }
    public virtual ArmorResult Run()
    {
        LocalKey source = ResolveLocalKey.Resolve(envFile ?? ".env", true);
        string? publicKey = source.PublicKey;
        string? privateKey = source.PrivateKey;

        JsonNode json = ArmorTeam.Run(team, chosen =>
            ArmorApi.ArmorUp(hostname, token, devicePublicKey, publicKey, privateKey, chosen,
                omitPrivateKey: privateKey is null));

        source.Remove();

        return new ArmorResult(ArmorTeam.AsBool(json["changed"]) || !string.IsNullOrEmpty(privateKey),
            source.PrivateKeyName, ArmorTeam.AsString(json["private_key"]), publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/armorDown.js</remarks>
public class ArmorDown(string hostname, string? token, string devicePublicKey, string? envFile = null, string? team = null)
{
    public virtual ArmorResult Run()
    {
        string file = envFile ?? ".env";
        string privateKeyName = GuessPrivateKeyName(file);
        string publicKey = ReadEnvKey(GuessPublicKeyName(file), file, true, ["MISSING_PRIVATE_KEY"])!;

        JsonNode json = ArmorTeam.Run(team, chosen =>
            ArmorApi.ArmorDown(hostname, token, devicePublicKey, publicKey, chosen));

        // `upsertEnvKey(privateKeyName, json.private_key)`: an absent value is written as "undefined"
        UpsertEnvKey(privateKeyName, ArmorTeam.AsString(json["private_key"]) ?? "undefined");

        return new ArmorResult(ArmorTeam.AsBool(json["changed"]), privateKeyName,
            ArmorTeam.AsString(json["private_key"]), publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/armorPull.js</remarks>
public class ArmorPull(string hostname, string? token, string devicePublicKey, string? envFile = null, string? team = null)
{
    public virtual ArmorResult Run()
    {
        string file = envFile ?? ".env";
        string privateKeyName = GuessPrivateKeyName(file);
        string publicKey = ReadEnvKey(GuessPublicKeyName(file), file, true, ["MISSING_PRIVATE_KEY"])!;

        JsonNode json = ArmorTeam.Run(team, chosen =>
            ArmorApi.ArmorPull(hostname, token, devicePublicKey, publicKey, chosen));

        UpsertEnvKeyResult upserted = UpsertEnvKey(privateKeyName, ArmorTeam.AsString(json["private_key"]) ?? "undefined");

        // armorPull.js: `changed: result.changed` (the file changed), not the API's own flag
        return new ArmorResult(upserted.Changed, privateKeyName,
            ArmorTeam.AsString(json["private_key"]), publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/armorPush.js</remarks>
public class ArmorPush(string hostname, string? token, string devicePublicKey, string? envFile = null, string? team = null)
{
    private static readonly Func<string, string?, string, string?, string?, ArmorPush> DefaultFactory =
        (hostname, token, devicePublicKey, envFile, team) => new ArmorPush(hostname, token, devicePublicKey, envFile, team);

    public static Func<string, string?, string, string?, string?, ArmorPush> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }
    public virtual ArmorResult Run()
    {
        LocalKey source = ResolveLocalKey.Resolve(envFile ?? ".env");
        string privateKey = source.PrivateKey!;

        JsonNode json = ArmorTeam.Run(team, chosen =>
            ArmorApi.ArmorPush(hostname, token, devicePublicKey, privateKey, chosen));

        return new ArmorResult(ArmorTeam.AsBool(json["changed"]), source.PrivateKeyName,
            ArmorTeam.AsString(json["private_key"]), source.PublicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/armorMove.js</remarks>
public class ArmorMove(string hostname, string? token, string devicePublicKey, string? envFile = null)
{
    public virtual ArmorResult Run()
    {
        string file = envFile ?? ".env";
        string privateKeyName = GuessPrivateKeyName(file);
        string publicKey = ReadEnvKey(GuessPublicKeyName(file), file, true, ["MISSING_PRIVATE_KEY"])!;

        JsonNode account = ArmorApi.GetAccount(hostname, token);
        Prompts.Choice[] choices =
        [
            .. (account["organizations"]?.AsArray() ??
                throw new InvalidOperationException("Cannot read properties of undefined (reading 'map')"))
            .Select(o => new Prompts.Choice(o!["provider_slug"]!.ToString(), o["provider_slug"]!.ToString()))
        ];

        string team = Prompts.Select(new Prompts.SelectRequest("Select team", choices) { Prefix = "⛨" }) ?? throw new PromptCancelledException();
        JsonNode json = ArmorApi.ArmorMove(hostname, token, devicePublicKey, publicKey, team);

        return new ArmorResult(ArmorTeam.AsBool(json["changed"]), privateKeyName,
            ArmorTeam.AsString(json["private_key"]), publicKey, ArmorTeam.AsString(json["team"]));
    }
}

public record ArmorOpenResult(string Url, string PublicKeyName, string PublicKeyValue);

/// <remarks>Reference source: dotenvx/src/lib/services/armorOpen.js</remarks>
public class ArmorOpen(string hostname, string? envFile = null)
{
    public virtual ArmorOpenResult Run()
    {
        string file = envFile ?? ".env";
        string publicKeyName = GuessPublicKeyName(file);
        string publicKey = ReadEnvKey(publicKeyName, file, true)!;

        return new ArmorOpenResult($"{hostname}/go/arm_{publicKey}", publicKeyName, publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/armorKeyring.js (access approval polling)</remarks>
public class ArmorKeyring(string? hostname, string? token, string devicePublicKey, string? publicKey)
{
    private const int PollIntervalMs = 1000;
    private const int PollTimeoutMs = 5 * 60 * 1000;

    /// <summary>Set by the provider when a status callback exists (approval uri shown to the user).</summary>
    public Action<string, string>? OnApprovalRequired { get; set; }

    private JsonNode Request(string? grantToken)
    {
        return ArmorApi.ArmorKeyring(hostname, token, devicePublicKey, publicKey, grantToken);
    }

    public virtual JsonNode Run()
    {
        try
        {
            return Request(null);
        }
        catch (ApiException error) when (error.Code == "ACCESS_APPROVAL_REQUIRED")
        {
            string? grantToken = error.Meta?["grant_token"]?.ToString();
            if (string.IsNullOrEmpty(grantToken))
            {
                throw;
            }

            string? approvalUri = error.Meta is not null
                ? error.Meta["approval_uri"]?.ToString()
                : error.Json?["error"]?["approval_uri"]?.ToString();
            if (string.IsNullOrEmpty(approvalUri))
            {
                throw;
            }

            OnApprovalRequired?.Invoke(approvalUri, error.Code);

            return Poll(grantToken);
        }
    }

    private JsonNode Poll(string grantToken)
    {
        long startedAt = Environment.TickCount64;

        while (true)
        {
            try
            {
                return Request(grantToken);
            }
            catch (ApiException error) when (error.Code is "ACCESS_PENDING" or "ACCESS_APPROVAL_PENDING")
            {
                if (Environment.TickCount64 - startedAt >= PollTimeoutMs)
                {
                    throw new ApiException("ACCESS_APPROVAL_TIMEOUT",
                        "[ACCESS_APPROVAL_TIMEOUT] approval timed out after 5 minutes");
                }

                Thread.Sleep(PollIntervalMs);
            }
        }
    }
}

public record LoginStart(
    string DeviceCode, string UserCode, string VerificationUri, string VerificationUriComplete, int Interval);

/// <remarks>Reference source: dotenvx/src/lib/services/login.js</remarks>
public class Login(string hostname, Session? session = null)
{
    private readonly Session _session = session ?? new Session();

    public virtual LoginStart Run()
    {
        string devicePublicKey = _session.DevicePublicKey();
        SystemInformation.Info system = _session.SystemInformation();
        JsonNode data = ArmorApi.OauthDeviceCode(hostname, devicePublicKey, system);

        return new LoginStart(data["device_code"]?.ToString() ?? "undefined", data["user_code"]?.ToString() ?? "undefined",
            data["verification_uri"]?.ToString() ?? "undefined",
            data["verification_uri_complete"]?.ToString() ?? "undefined", (int)(data["interval"]?.GetValue<double>() ?? 0));
    }

    public virtual JsonNode Poll(string deviceCode, int interval)
    {
        while (true)
        {
            try
            {
                JsonNode data = ArmorApi.OauthToken(hostname, deviceCode);
                if (!string.IsNullOrEmpty(data["access_token"]?.ToString()))
                {
                    _session.Login(hostname, data["id"]?.ToString(), data["username"]?.ToString(),
                        data["access_token"]!.ToString());
                    _session.NotifyUpdate();

                    return data;
                }

                Thread.Sleep(interval * 1000);
            }
            catch (ApiException error) when (error.Code == "authorization_pending")
            {
                Thread.Sleep((interval + 1) * 1000);
            }
        }
    }
}

public record LogoutResult(string? Username, string? AccessToken, string SettingsDevicesUrl);

/// <remarks>Reference source: dotenvx/src/lib/services/logout.js</remarks>
public class Logout(string hostname, Session? session = null)
{
    private readonly Session _session = session ?? new Session();

    public virtual LogoutResult Run()
    {
        string? token = _session.Token();
        JsonNode data = ArmorApi.Logout(hostname, token);
        string? id = data["id"]?.ToString();
        string? username = data["username"]?.ToString();
        string? accessToken = data["access_token"]?.ToString();
        string settingsDevicesUrl = $"{hostname}/settings/devices";

        _session.Logout(hostname, id, accessToken);

        return new LogoutResult(username, accessToken, settingsDevicesUrl);
    }
}
