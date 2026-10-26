using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Providers;

/// <summary>The wire format shared by every Armor call (armorRequest.js, buildApiError.js).</summary>
internal static class ArmorWire
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Stringify(JsonNode body)
    {
        return body.ToJsonString(JsonOptions);
    }

    /// <summary>`await resp.body.json()`: invalid JSON raises V8's SyntaxError text for the common cases.</summary>
    public static JsonNode ParseJson(string body)
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

    private static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(30);

    /// <summary>armorRequest.js: POST `{hostname}/api/armor/{endpoint}` with the device public key and CLI version.</summary>
    internal static JsonNode Request(string endpoint, ProviderOptions options, JsonObject body)
    {
        JsonObject payload = new() { ["device_public_key"] = options.DevicePublicKey };
        if (options.CliVersion is not null)
        {
            payload["cli_version"] = options.CliVersion;
        }

        foreach ((string key, JsonNode? value) in body)
        {
            payload[key] = value?.DeepClone();
        }

        string host = string.IsNullOrEmpty(options.Hostname) ? "https://armor.dotenvx.com" : options.Hostname;
        try
        {
            // each request is bounded, body included; approval polling has its own deadline
            HttpResult response = ArmorHttp.RequestWithin(RequestDeadline, $"{host}/api/armor/{endpoint}", "POST",
                new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {options.Token ?? ""}",
                    ["Content-Type"] = "application/json"
                }, Stringify(payload));
            JsonNode json = ParseJson(response.Body);
            if (response.StatusCode >= 400)
            {
                throw BuildApiError(response.StatusCode, json);
            }

            return json;
        }
        catch (Exception error) when (error is HttpRequestFailedException { Code: ArmorHttp.AbortedCode } ||
                                      ArmorHttp.IsNetworkError(error))
        {
            string reason = error is HttpRequestFailedException { Code: ArmorHttp.AbortedCode }
                ? "Armor request timed out after 30 seconds"
                : "unable to reach Armor";

            throw new ArmorUnreachableException(reason, "check your connection or use --no-armor", error);
        }
    }
}

/// <summary>The `context` of an Armor decrypt: the public key and an approved grant token.</summary>
public sealed record ArmorDecryptContext(string? PublicKey = null, string? GrantToken = null);

/// <remarks>
/// Reference source: @dotenvx/providers armor.js, armorSession.js, armorKeyring.js and armorRequest.js. The synchronous
/// forms run the same steps on the calling thread (no UI callbacks, like upstream's worker).
/// </remarks>
public sealed class ArmorProvider(ProviderOptions options) : IKeyProvider
{
    private const string Pending = "ACCESS_PENDING";
    private const string ApprovalPending = "ACCESS_APPROVAL_PENDING";
    private const string ApprovalRequiredCode = "ACCESS_APPROVAL_REQUIRED";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(5);

    private static ProviderException NotConfigured()
    {
        return new ProviderException("ARMOR_NOT_CONFIGURED", "[ARMOR_NOT_CONFIGURED] sign in to Armor");
    }

    private static bool Disabled(ProviderOptions options)
    {
        return options.ArmorOff || options.NoArmor || options.Env("DOTENVX_NO_ARMOR") == "true";
    }

    private static bool Complete(ProviderOptions options)
    {
        return !string.IsNullOrEmpty(options.Token) && !string.IsNullOrEmpty(options.Hostname) &&
               !string.IsNullOrEmpty(options.DevicePublicKey);
    }

    private static string DevicePublicKey(ProviderOptions options)
    {
        string? cwd = options.Env("DOTENVX_CONFIG");

        return new Device(string.IsNullOrEmpty(cwd) ? null : cwd).PublicKey();
    }

    private static ProviderOptions? Resolve(ProviderOptions options, Dictionary<string, string?> saved)
    {
        if (string.IsNullOrEmpty(options.Token) && (saved["ON"] == "false" || string.IsNullOrEmpty(saved["USERNAME"])))
        {
            return null;
        }

        string? token = options.Token;
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                token = NativeProvider.Platform is "darwin" or "linux" or "win32"
                    ? NativeProvider.Backend("DOTENVX_ARMOR_TOKEN")
                    : null;
            }
            catch (Exception)
            {
                token = null;
            }

            if (string.IsNullOrEmpty(token))
            {
                token = saved["TOKEN"];
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        string hostname = !string.IsNullOrEmpty(options.Hostname) ? options.Hostname
            : !string.IsNullOrEmpty(saved["HOSTNAME"]) ? saved["HOSTNAME"]! : "https://armor.dotenvx.com";
        string device = !string.IsNullOrEmpty(options.DevicePublicKey) ? options.DevicePublicKey : DevicePublicKey(options);

        return options.With(hostname, token, device);
    }

    /// <summary>`session(options)`: null when Armor is disabled or not signed in.</summary>
    internal static ProviderOptions? Session(ProviderOptions options)
    {
        if (Disabled(options))
        {
            return null;
        }

        if (Complete(options))
        {
            return options;
        }

        Dictionary<string, string?> saved = [];
        foreach (string key in new[] { "HOSTNAME", "ON", "USERNAME", "TOKEN" })
        {
            saved[key] = ProviderSettings.GetSync($"DOTENVX_ARMOR_{key}", options);
        }

        return Resolve(options, saved);
    }

    private static string? GrantToken(ApiException error)
    {
        return error.Meta?["grant_token"]?.ToString() is { Length: > 0 } token ? token : null;
    }

    private static string? ApprovalUri(ApiException error)
    {
        string? uri = error.Meta is not null
            ? error.Meta["approval_uri"]?.ToString()
            : error.Json?["error"]?["approval_uri"]?.ToString();

        return string.IsNullOrEmpty(uri) ? null : uri;
    }

    private static IDictionary<string, string> ToRing(JsonNode json)
    {
        Dictionary<string, string> ring = [];
        if (json is JsonObject obj)
        {
            foreach ((string key, JsonNode? value) in obj)
            {
                if (value is not null)
                {
                    ring[key] = value is JsonValue v && v.TryGetValue(out string? text) ? text : value.ToJsonString();
                }
            }
        }

        return ring;
    }

    private static ProviderException ApprovalTimeout()
    {
        return new ProviderException("ACCESS_APPROVAL_TIMEOUT",
            "[ACCESS_APPROVAL_TIMEOUT] approval timed out after 5 minutes");
    }

    private static bool IsPending(ApiException error)
    {
        return error.Code is Pending or ApprovalPending;
    }

    /// <summary>
    /// approvalRequired.js: the default message when no `onApprovalRequired` callback is supplied (written straight to
    /// stderr, so it stays visible while a synchronous lookup blocks its caller).
    /// </summary>
    public static void ApprovalRequired(ApprovalRequest info)
    {
        Console.Error.Write($"[{info.Code}] open [{info.ApprovalUri}] to approve. Waiting for approval…\n");
        Console.Error.Flush();
    }

    /// <summary>
    /// ArmorApprovalRequest: POST `operation`; when the response asks for approval, report the URL and poll with the grant
    /// token. Callbacks are only supported by asynchronous lookups (a synchronous one always prints the default message).
    /// </summary>
    private static async Task<JsonNode> WithApproval(ProviderOptions session, string operation, JsonObject body,
        bool allowCallbacks)
    {
        string? grantToken;
        string? approvalUri;
        try
        {
            return ArmorWire.Request(operation, session, WithGrant(body, null));
        }
        catch (ApiException error) when (error.Code == ApprovalRequiredCode &&
                                         (grantToken = GrantToken(error)) is not null &&
                                         (approvalUri = ApprovalUri(error)) is not null)
        {
            Action<ApprovalRequest> report = allowCallbacks && session.OnApprovalRequired is not null
                ? session.OnApprovalRequired
                : ApprovalRequired;
            report(new ApprovalRequest(approvalUri, error.Code));

            DateTime startedAt = DateTime.UtcNow;
            while (true)
            {
                try
                {
                    return ArmorWire.Request(operation, session, WithGrant(body, grantToken));
                }
                catch (ApiException poll) when (IsPending(poll))
                {
                    if (DateTime.UtcNow - startedAt >= PollTimeout)
                    {
                        throw ApprovalTimeout();
                    }

                    await Task.Delay(PollInterval);
                }
            }
        }
    }

    private static JsonObject WithGrant(JsonObject body, string? grantToken)
    {
        JsonObject copy = (JsonObject)body.DeepClone();
        if (!string.IsNullOrEmpty(grantToken))
        {
            copy["grant_token"] = grantToken;
        }

        return copy;
    }

    public async Task<IDictionary<string, string>> Get(string publicKey)
    {
        ProviderOptions? session = Session(options);

        return session is null
            ? ProviderErrors.Empty()
            : ToRing(await WithApproval(session, "keyring", new JsonObject { ["public_key"] = publicKey }, true));
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        ProviderOptions? session = Session(options);

        return session is null
            ? ProviderErrors.Empty()
            : ToRing(WithApproval(session, "keyring", new JsonObject { ["public_key"] = publicKey }, false)
                .GetAwaiter().GetResult());
    }

    private static JsonObject DecryptBody(string src, ArmorDecryptContext context)
    {
        JsonObject body = new() { ["public_key"] = context.PublicKey, ["src"] = src };
        if (!string.IsNullOrEmpty(context.GrantToken))
        {
            body["grant_token"] = context.GrantToken;
        }

        return body;
    }

    public async Task<JsonNode> Decrypt(string src, ArmorDecryptContext? context = null)
    {
        ProviderOptions session = Session(options) ?? throw NotConfigured();

        return await WithApproval(session, "decrypt", DecryptBody(src, context ?? new ArmorDecryptContext()), true);
    }

    public JsonNode DecryptSync(string src, ArmorDecryptContext? context = null)
    {
        ProviderOptions session = Session(options) ?? throw NotConfigured();

        return WithApproval(session, "decrypt", DecryptBody(src, context ?? new ArmorDecryptContext()), false)
            .GetAwaiter().GetResult();
    }
}
