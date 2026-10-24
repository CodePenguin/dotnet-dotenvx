using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <summary>One validation failure of a key.</summary>
public sealed record CheckError(string Key, string Code, string Rule, string Message, bool Strict);

/// <summary>The result of <see cref="Primitives.Check" />: the values with secrets redacted, and the failures.</summary>
public sealed class CheckResult
{
    public OrderedDictionary<string, string> Checked { get; init; } = [];
    public List<CheckError> Errors { get; init; } = [];
}

/// <remarks>
/// Counterpart of the @dotenvx/primitives `check` and `redact` exports. `check` validates final string values against a
/// compiled policy and returns them redacted (unless the key is explicitly public); `redact` replaces literal secrets.
/// </remarks>
public static partial class Primitives
{
    private static readonly Regex EmailLocal = new(@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*\z",
        RegexOptions.CultureInvariant);

    private static readonly Regex HostLabel = new(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\z", RegexOptions.CultureInvariant);
    private static readonly Regex TopLevelLabel = new(@"^(?:[A-Za-z]{2,63}|[xX][nN]--[A-Za-z0-9-]+)\z", RegexOptions.CultureInvariant);

    public static CheckResult Check(EnvPolicy policy, IReadOnlyDictionary<string, string> values)
    {
        // Object.entries order of the values: integer-like keys ascending, then the others in insertion order
        List<KeyValuePair<string, string>> entries = JsKeyOrder(values);
        HashSet<string> secrets = [];
        List<string> plaintextKeys = [];
        foreach ((string key, string value) in entries)
        {
            bool plaintext = policy.Keys.TryGetValue(key, out KeyPolicy? rule) && !rule.Redacted;
            if (!plaintext)
            {
                secrets.Add(value);
            }
            else
            {
                plaintextKeys.Add(key);
            }
        }

        List<string> secretList = [.. secrets];
        CheckResult result = new();
        foreach ((string key, string value) in entries)
        {
            bool plaintext = plaintextKeys.Contains(key);
            result.Checked[key] = !plaintext && value == "" ? "[REDACTED]" : Redact(value, secretList);
        }

        foreach ((string key, KeyPolicy rule) in policy.Keys)
        {
            EnvRule env = rule.Env;
            void Add(string code, string ruleName, string message)
            {
                result.Errors.Add(new CheckError(key, code, ruleName, message, env.Strict));
            }

            bool has = values.TryGetValue(key, out string? value);
            if (!has || JsTrim(value!).Length == 0)
            {
                if (env.Required)
                {
                    Add("MISSING_REQUIRED", "required", $"{key} is required");
                }

                continue;
            }

            bool numeric = env.Type is "integer" or "port";
            if (!ValidType(env.Type, value!))
            {
                string kind = numeric ? "integer" : env.Type;
                Add($"INVALID_{kind.ToUpperInvariant()}", env.Type, $"{key} must be a valid {kind}");
                continue;
            }

            if (env.Enum != null && !env.Enum.Any(choice =>
                    numeric ? BigInteger.Parse(JsTrim(choice)) == BigInteger.Parse(JsTrim(value!)) : choice == value))
            {
                Add("INVALID_ENUM", "enum", $"{key} is not an allowed value");
            }

            if (numeric || env.Type == "string")
            {
                BigInteger size = numeric ? BigInteger.Parse(JsTrim(value!)) : CodePointCount(value!);
                string suffix = numeric ? "" : " characters long";
                if (env.Min != null && size < BigInteger.Parse(env.Min))
                {
                    Add("BELOW_MIN", env.Type == "port" ? "port" : "min", $"{key} must be at least {env.Min}{suffix}");
                }

                if (env.Max != null && size > BigInteger.Parse(env.Max))
                {
                    Add("ABOVE_MAX", env.Type == "port" ? "port" : "max", $"{key} must be at most {env.Max}{suffix}");
                }
            }
        }

        return result;
    }

    /// <summary>The order JavaScript's Object.entries gives for property names: array indexes ascending, then the rest as inserted.</summary>
    internal static List<KeyValuePair<string, string>> JsKeyOrder(IEnumerable<KeyValuePair<string, string>> values)
    {
        List<KeyValuePair<string, string>> all = [.. values];
        List<KeyValuePair<string, string>> indexes = [.. all.Where(pair => IsArrayIndex(pair.Key))];
        indexes.Sort((a, b) => ulong.Parse(a.Key).CompareTo(ulong.Parse(b.Key)));
        return [.. indexes, .. all.Where(pair => !IsArrayIndex(pair.Key))];
    }

    private static bool IsArrayIndex(string key)
    {
        return key.Length > 0 && key.Length <= 10 && key.All(char.IsAsciiDigit) && (key == "0" || key[0] != '0') &&
               ulong.Parse(key) < 4294967295UL;
    }

    private static int CodePointCount(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static bool ValidType(string type, string value)
    {
        return type switch
        {
            "string" => true,
            "integer" or "port" => IsInteger(value),
            "boolean" => IsBooleanValue(value),
            "url" => IsUrl(value),
            "email" => IsEmail(value),
            "ip" => IsIp(value),
            _ => throw new ArgumentException("Unsupported policy type")
        };
    }

    /// <summary>Replaces every literal occurrence of a secret (overlapping matches merge) with <c>[REDACTED]</c>.</summary>
    public static string Redact(string text, IEnumerable<string> secrets)
    {
        List<(int Start, int End)> ranges = [];
        foreach (string secret in secrets.Where(secret => secret.Length > 0).Distinct())
        {
            int index = text.IndexOf(secret, StringComparison.Ordinal);
            while (index != -1)
            {
                ranges.Add((index, index + secret.Length));
                index = text.IndexOf(secret, index + 1, StringComparison.Ordinal);
            }
        }

        ranges.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));
        List<(int Start, int End)> merged = [];
        foreach ((int start, int end) in ranges)
        {
            if (merged.Count > 0 && start < merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, end));
            }
            else
            {
                merged.Add((start, end));
            }
        }

        StringBuilder result = new();
        int cursor = 0;
        foreach ((int start, int end) in merged)
        {
            result.Append(text, cursor, start - cursor).Append("[REDACTED]");
            cursor = end;
        }

        return result.Append(text, cursor, text.Length - cursor).ToString();
    }

    // ---- type validators (email, ip: the regular expressions of the package and of node:net; url: the WHATWG URL parser)

    internal static bool IsEmail(string value)
    {
        if (value.Length > 254 || value.Any(c => JsWhitespaceChars.Contains(c)))
        {
            return false;
        }

        string[] parts = value.Split('@');
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Length > 64 || !EmailLocal.IsMatch(parts[0]))
        {
            return false;
        }

        string[] labels = parts[1].Split('.');
        return labels.Length > 1 && labels.All(label => HostLabel.IsMatch(label)) && TopLevelLabel.IsMatch(labels[^1]);
    }

    private const string V4Segment = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9][0-9]|[0-9])";
    private const string V4 = "(?:" + V4Segment + @"\.){3}" + V4Segment;
    private const string V6Segment = "(?:[0-9a-fA-F]{1,4})";

    private static readonly Regex Ipv4Regex = new("^" + V4 + @"\z", RegexOptions.CultureInvariant);

    private static readonly Regex Ipv6Regex = new(
        "^(?:" +
        $"(?:{V6Segment}:){{7}}(?:{V6Segment}|:)|" +
        $"(?:{V6Segment}:){{6}}(?:{V4}|:{V6Segment}|:)|" +
        $"(?:{V6Segment}:){{5}}(?::{V4}|(?::{V6Segment}){{1,2}}|:)|" +
        $"(?:{V6Segment}:){{4}}(?:(?::{V6Segment}){{0,1}}:{V4}|(?::{V6Segment}){{1,3}}|:)|" +
        $"(?:{V6Segment}:){{3}}(?:(?::{V6Segment}){{0,2}}:{V4}|(?::{V6Segment}){{1,4}}|:)|" +
        $"(?:{V6Segment}:){{2}}(?:(?::{V6Segment}){{0,3}}:{V4}|(?::{V6Segment}){{1,5}}|:)|" +
        $"(?:{V6Segment}:){{1}}(?:(?::{V6Segment}){{0,4}}:{V4}|(?::{V6Segment}){{1,6}}|:)|" +
        $"(?::(?:(?::{V6Segment}){{0,5}}:{V4}|(?::{V6Segment}){{1,7}}|:))" +
        @")(?:%[0-9a-zA-Z\-.:]{1,})?\z", RegexOptions.CultureInvariant);

    /// <summary>node:net `isIP(value) !== 0`.</summary>
    internal static bool IsIp(string value)
    {
        return Ipv4Regex.IsMatch(value) || Ipv6Regex.IsMatch(value);
    }

    /// <summary>`new URL(value)` succeeds (an absolute URL in the WHATWG sense).</summary>
    internal static bool IsUrl(string input)
    {
        return WhatwgUrl.IsValid(input);
    }
}
