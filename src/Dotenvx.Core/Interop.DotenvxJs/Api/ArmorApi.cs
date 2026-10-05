using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Api;

/// <summary>An error response from the Armor API (helpers/buildApiError.js and buildOauthError.js).</summary>
public class ApiException(string code, string message, string? help = null)
    : Types.DotenvxException(code, message, help)
{
    /// <summary>`error.help`: always set by buildApiError.js (the `fix: [..]` text, or `[code] &lt;json&gt;` when the API sent none).</summary>
    public string? ErrorHelp { get; init; }

    public JsonNode? Meta { get; init; }
    public JsonNode? Json { get; init; }
    public int StatusCode { get; init; }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/api/*.js (postArmorUp/Down/Pull/Push/Move/Keyring/Decrypt, postLogout, getAccount,
/// postOauthDeviceCode, postOauthToken). Each endpoint builds the same JSON body in the same key order, sends it through
/// <see cref="ArmorHttp.Request" /> and returns the parsed JSON; a status of 400 or more raises the error
/// buildApiError.js / buildOauthError.js would.
/// </remarks>
public static class ArmorApi
{
    private const string OauthClientId = "oac_dotenvxcli";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Stringify(JsonNode body)
    {
        return body.ToJsonString(JsonOptions);
    }

    /// <summary>`await resp.body.json()`: invalid JSON raises V8's SyntaxError text for the common cases.</summary>
    private static JsonNode ParseJson(string body)
    {
        if (body.Length == 0)
        {
            throw new FormatException("Unexpected end of JSON input");
        }

        try
        {
            return JsonNode.Parse(body) ?? throw new FormatException("Unexpected end of JSON input");
        }
        catch (JsonException)
        {
            char first = body.TrimStart()[0];
            string preview = body.Length <= 10 ? body : body[..10] + "\"...";
            throw new FormatException(
                body.Length <= 10
                    ? $"Unexpected token '{first}', \"{preview}\" is not valid JSON"
                    : $"Unexpected token '{first}', \"{preview} is not valid JSON");
        }
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
        JsonNode? error = json["error"] ?? throw new NullReferenceException("Cannot read properties of undefined");
        string? errorCode = error["code"]?.ToString();
        string code = string.IsNullOrEmpty(errorCode) ? statusCode.ToString() : errorCode;
        string message = $"[{code}] {error["message"]}";
        string? helpText = error["help"]?.ToString();
        bool hasHelp = !string.IsNullOrEmpty(helpText);
        string help = hasHelp ? $"fix: [{helpText}]" : $"[{code}] {json.ToJsonString(JsonOptions)}";

        // `error.messageWithHelp` only exists when the API sent a help text (so `MessageWithHelp` is the plain message otherwise)
        return new ApiException(code, message, hasHelp ? help : null)
        {
            ErrorHelp = help,
            Meta = error["meta"],
            Json = json,
            StatusCode = statusCode
        };
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
