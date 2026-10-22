using System.Collections;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Events;

/// <remarks>
/// Reference source: dotenvx/src/lib/events/metadata.js and catalog.js. Event metadata is an allowlist, not a copy of command
/// options or debug logs. Values are JS-like: null, bool, numbers, strings and arrays of those (anything else is dropped).
/// </remarks>
internal static partial class EventMetadata
{
    private static readonly string[] Fields =
    [
        "key", "file", "files", "changed", "output", "action", "phase", "decision", "reason", "error_code", "error_count",
        "warning_count", "injected_count", "proxied_count", "exit_code", "signal", "duration_ms", "filter", "ignore", "scope",
        "executable"
    ];

    private static readonly string[] OptionFields =
    [
        "envFile", "envKeysFile", "key", "includeKey", "excludeKey", "stdout", "plain", "armor", "native", "1password",
        "bitwarden", "create", "overload", "all", "strict", "convention", "ignore", "mask", "redact", "format", "prettyPrint",
        "pp", "docker", "gitFile", "gitProcess", "quiet", "verbose", "debug", "logLevel", "path", "encoding", "override",
        "encrypt", "noArmor", "noNative", "no1Password", "noBitwarden"
    ];

    /// <summary>Public operations (catalog.js): name => whether the first command argument is recorded as `key`.</summary>
    public static readonly IReadOnlyDictionary<string, bool> Catalog = new Dictionary<string, bool>
    {
        ["cli/run"] = false,
        ["cli/get"] = true,
        ["cli/set"] = true,
        ["cli/del"] = true,
        ["cli/encrypt"] = false,
        ["cli/decrypt"] = false,
        ["cli/keypair"] = true,
        ["cli/protect"] = false,
        ["sdk/config"] = false,
        ["sdk/get"] = true,
        ["sdk/set"] = true
    };

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,79}$")]
    private static partial Regex ErrorCodePattern();

    [GeneratedRegex("^([A-Z][A-Z0-9_]*):")]
    private static partial Regex MessageCodePattern();

    /// <summary>metadata.js `value`: null when the input is dropped (`undefined` or unsupported), else the JSON value.</summary>
    private static bool TryValue(object? input, out JsonNode? node)
    {
        node = null;
        switch (input)
        {
            case null:
                return true;
            case bool flag:
                node = JsonValue.Create(flag);

                return true;
            case string text:
                node = JsonValue.Create(text.Length > 1024 ? text[..1024] : text);

                return true;
            case int or long or short or byte or uint or ulong:
                node = JsonValue.Create(Convert.ToInt64(input));

                return true;
            case double number when double.IsFinite(number):
                node = JsonValue.Create(number);

                return true;
            case IEnumerable items and not IDictionary:
                var array = new JsonArray();
                int count = 0;
                foreach (object? item in items)
                {
                    if (count++ >= 100)
                    {
                        break;
                    }

                    // nested arrays and unsupported items are filtered out; null items stay
                    if (item is IEnumerable and not string and not IDictionary || !TryValue(item, out JsonNode? inner))
                    {
                        continue;
                    }

                    array.Add(inner);
                }

                node = array;

                return true;
            default:
                return false;
        }
    }

    private static JsonObject Select(IReadOnlyDictionary<string, object?> input, string[] allowed)
    {
        var output = new JsonObject();
        foreach (string key in allowed)
        {
            if (input.TryGetValue(key, out object? raw) && TryValue(raw, out JsonNode? selected))
            {
                output[key] = selected;
            }
        }

        return output;
    }

    public static JsonObject Metadata(IReadOnlyDictionary<string, object?> input)
    {
        return Select(input, Fields);
    }

    public static JsonObject Options(IReadOnlyDictionary<string, object?> input)
    {
        return Select(input, OptionFields);
    }

    /// <summary>`error.code` when it looks like a code (`^[A-Z][A-Z0-9_]{0,79}$`), else `COMMAND_FAILED`.</summary>
    public static string ErrorCode(Exception? error)
    {
        string? code = error is null ? null : Helpers.Helpers.ErrorCodeOf(error);
        if (code is null && error is not null && MessageCodePattern().Match(error.Message) is { Success: true } match)
        {
            code = match.Groups[1].Value; // raw fs errors keep Node's "ENOENT: ..." text, and so its code
        }

        return code is not null && ErrorCodePattern().IsMatch(code) ? code : "COMMAND_FAILED";
    }

    public static string ErrorCode(string? code)
    {
        return code is not null && ErrorCodePattern().IsMatch(code) ? code : "COMMAND_FAILED";
    }
}
