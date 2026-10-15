using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Api;

/// <remarks>
/// Reference source: dotenvx/src/lib/api/*.js (postArmorUp/Down/Pull/Push/Move/Keyring/Decrypt, postLogout, getAccount,
/// postOauthDeviceCode, postOauthToken). Each endpoint builds the same JSON body in the same key order, sends it through
/// <see cref="ArmorHttp.Request" /> and returns the parsed JSON; a status of 400 or more raises the error
/// buildApiError.js / buildOauthError.js would.
/// </remarks>
public static class ArmorApi
{
    private const string OauthClientId = "oac_dotenvxcli";

    private static string Stringify(JsonNode body)
    {
        return ArmorWire.Stringify(body);
    }

    private static JsonNode ParseJson(string body)
    {
        return ArmorWire.ParseJson(body);
    }

    private static IReadOnlyDictionary<string, string> BearerHeaders(string? token)
    {
        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token ?? ""}", ["Content-Type"] = "application/json"
        };
    }

    public static ApiException BuildApiError(int statusCode, JsonNode json)
    {
        return ArmorWire.BuildApiError(statusCode, json);
    }

    public static ApiException BuildOauthError(int statusCode, JsonNode json)
    {
        string code = json["error"]?.ToString() ?? "";

        return new ApiException(code, $"[{code}] {json["error_description"]}")
        {
            Json = json, StatusCode = statusCode
        };
    }

    private static JsonNode Call(string url, string method, IReadOnlyDictionary<string, string> headers, string? body,
        Func<int, JsonNode, ApiException> buildError)
    {
        HttpResult response = ArmorHttp.Request(url, method, headers, body);
        JsonNode json = ParseJson(response.Body);

        if (response.StatusCode >= 400)
        {
            throw buildError(response.StatusCode, json);
        }

        return json;
    }

    private static JsonNode Api(string url, string method, string? token, JsonNode? body)
    {
        return Call(url, method, BearerHeaders(token), body is null ? null : Stringify(body), BuildApiError);
    }

    private static JsonObject ArmorBody(string devicePublicKey)
    {
        return new JsonObject { ["device_public_key"] = devicePublicKey, ["cli_version"] = PackageJson.Version };
    }

    public static JsonNode ArmorUp(string hostname, string? token, string devicePublicKey, string? publicKey,
        string? privateKey, string? team, bool omitPrivateKey = false)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        // an undefined value (a key missing locally) is dropped by JSON.stringify, a null one is sent as null
        if (publicKey is not null)
        {
            body["public_key"] = publicKey;
        }

        if (!omitPrivateKey)
        {
            body["private_key"] = privateKey;
        }

        AddTeam(body, team);

        return Api($"{hostname}/api/armor/up", "POST", token, body);
    }

    public static JsonNode ArmorDown(string hostname, string? token, string devicePublicKey, string publicKey,
        string? team)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        body["public_key"] = publicKey;
        AddTeam(body, team);

        return Api($"{hostname}/api/armor/down", "POST", token, body);
    }

    public static JsonNode ArmorPull(string hostname, string? token, string devicePublicKey, string publicKey,
        string? team)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        body["public_key"] = publicKey;
        AddTeam(body, team);

        return Api($"{hostname}/api/armor/pull", "POST", token, body);
    }

    public static JsonNode ArmorPush(string hostname, string? token, string devicePublicKey, string privateKey,
        string? team)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        body["private_key"] = privateKey;
        AddTeam(body, team);

        return Api($"{hostname}/api/armor/push", "POST", token, body);
    }

    public static JsonNode ArmorMove(string hostname, string? token, string devicePublicKey, string publicKey,
        string? team)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        body["public_key"] = publicKey;
        AddTeam(body, team);

        return Api($"{hostname}/api/armor/move", "POST", token, body);
    }

    public static JsonNode ArmorKeyring(string? hostname, string? token, string devicePublicKey, string? publicKey,
        string? grantToken)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        if (!string.IsNullOrEmpty(publicKey))
        {
            body["public_key"] = publicKey;
        }

        if (!string.IsNullOrEmpty(grantToken))
        {
            body["grant_token"] = grantToken;
        }

        return Api($"{(string.IsNullOrEmpty(hostname) ? "https://armor.dotenvx.com" : hostname)}/api/armor/keyring",
            "POST", token, body);
    }

    public static JsonNode ArmorDecrypt(string? hostname, string? token, string devicePublicKey, string? publicKey,
        string src, string? grantToken)
    {
        JsonObject body = ArmorBody(devicePublicKey);
        // `public_key: this.publicKey`: an undefined key is dropped by JSON.stringify
        if (publicKey is not null)
        {
            body["public_key"] = publicKey;
        }

        body["src"] = src;
        if (!string.IsNullOrEmpty(grantToken))
        {
            body["grant_token"] = grantToken;
        }

        return Api($"{(string.IsNullOrEmpty(hostname) ? "https://armor.dotenvx.com" : hostname)}/api/armor/decrypt",
            "POST", token, body);
    }

    /// <summary>postFeedback.js: `POST /api/feedback` with `{ body }` (no authorization).</summary>
    public static JsonNode PostFeedback(string hostname, string body)
    {
        return Call($"{hostname}/api/feedback", "POST",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Stringify(new JsonObject { ["body"] = body }), BuildApiError);
    }

    public static JsonNode Logout(string hostname, string? token)
    {
        return Api($"{hostname}/api/logout", "POST", token, new JsonObject());
    }

    public static JsonNode GetAccount(string hostname, string? token)
    {
        // getAccount.js does not normalize a missing token: `Bearer undefined`
        return Call($"{hostname}/api/account", "GET",
            new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token ?? "undefined"}", ["Content-Type"] = "application/json"
            }, null, BuildApiError);
    }

    public static JsonNode OauthDeviceCode(string hostname, string devicePublicKey, SystemInformation.Info system)
    {
        var body = new JsonObject
        {
            ["client_id"] = OauthClientId,
            ["device_public_key"] = devicePublicKey,
            ["system_information"] = new JsonObject
            {
                ["system_uuid"] = system.SystemUuid, ["os_platform"] = system.OsPlatform, ["os_arch"] = system.OsArch
            },
            ["dotenvx_project_id"] = null
        };

        return Call($"{hostname}/oauth/device/code", "POST",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" }, Stringify(body), BuildOauthError);
    }

    public static JsonNode OauthToken(string hostname, string deviceCode)
    {
        var body = new JsonObject
        {
            ["client_id"] = OauthClientId,
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
        };

        return Call($"{hostname}/oauth/token", "POST",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" }, Stringify(body), BuildOauthError);
    }

    private static void AddTeam(JsonObject body, string? team)
    {
        if (team is not null)
        {
            body["team"] = team;
        }
    }
}
