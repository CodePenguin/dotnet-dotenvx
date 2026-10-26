using System.Text.Json.Nodes;

namespace Dotenvx;

/// <summary>An error response from the Armor API (helpers/buildApiError.js and buildOauthError.js).</summary>
public class ApiException(string code, string message, string? help = null)
    : DotenvxException(code, message, help)
{
    /// <summary>`error.help`: always set by buildApiError.js (the `fix: [..]` text, or `[code] &lt;json&gt;` when the API sent none).</summary>
    public string? ErrorHelp { get; init; }

    public JsonNode? Meta { get; init; }
    public JsonNode? Json { get; init; }
    public int StatusCode { get; init; }
}

/// <summary>
/// `[ARMOR_UNREACHABLE]` of @dotenvx/providers armorRequest.js: Armor could not be reached, or took longer than 30 seconds.
/// <see cref="ErrorHelp" /> is the `error.help` of the original (without the `fix: [..]` wrapper `messageWithHelp` adds).
/// </summary>
public class ArmorUnreachableException(string reason, string help, Exception? cause = null)
    : DotenvxException("ARMOR_UNREACHABLE", $"[ARMOR_UNREACHABLE] {reason}", $"fix: [{help}]")
{
    public string ErrorHelp { get; } = help;

    public Exception? Cause { get; } = cause;
}
