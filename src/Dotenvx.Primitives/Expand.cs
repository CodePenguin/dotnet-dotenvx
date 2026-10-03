using System.Text;
using System.Text.RegularExpressions;

namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `expand(value, { overload, processEnv, runningParsed, literals })`
/// export (read live from the minified source). Operator semantics: `:-` gives `env[k] || default`,
/// `-` gives `has ? env[k] : default`, `:+` gives `env[k] ? alt : ""`, `+` gives `has ? alt : ""`.
/// `String.replace(str, str)` only replaces the first occurrence and expands `$` patterns, hence
/// <see cref="JsReplacement" />. Moved here from Core's Parse engine.
/// </remarks>
public static partial class Primitives
{
    public static string Expand(string value, bool overload, IDictionary<string, string> processEnv,
        IDictionary<string, string> runningParsed, IDictionary<string, string> literals)
    {
        Dictionary<string, string> env;
        if (overload)
        {
            env = new Dictionary<string, string>(processEnv);
            foreach (KeyValuePair<string, string> p in runningParsed)
            {
                env[p.Key] = p.Value;
            }
        }
        else
        {
            env = new Dictionary<string, string>(runningParsed);
            foreach (KeyValuePair<string, string> p in processEnv)
            {
                env[p.Key] = p.Value;
            }
        }

        string result = value;
        Match match;

        while ((match = ExpandRegex().Match(result)).Success)
        {
            string template = match.Groups[0].Value;
            string expression = match.Groups[1].Success && match.Groups[1].Value.Length > 0
                ? match.Groups[1].Value
                : match.Groups[2].Value;

            // match the operators `:+`, `+`, `:-`, and `-` - find first match
            Match opMatch = OpRegex().Match(expression);
            string? splitter = opMatch.Success ? opMatch.Value : null;

            // JS split(null) splits on the literal text "null"
            string sep = splitter ?? "null";
            string[] parts = expression.Split([sep], StringSplitOptions.None);
            string key = parts[0];
            string rest = string.Join(sep, parts, 1, parts.Length - 1);

            bool has = env.TryGetValue(key, out string? current);
            bool truthy = !string.IsNullOrEmpty(current);
            string val = splitter switch
            {
                ":-" => truthy ? current! : rest,
                "-" => has ? current! : rest,
                ":+" => truthy ? rest : "",
                "+" => has ? rest : "",
                _ => truthy ? current! : ""
            };

            // JS String.replace(str, str): first occurrence only, with $-pattern expansion
            int at = result.IndexOf(template, StringComparison.Ordinal);
            result = string.Concat(result.AsSpan(0, at), JsReplacement(val, template, result, at),
                result.AsSpan(at + template.Length));

            // if the result equaled what was in env then stop expanding - handle self-referential check as well
            if (result == env.GetValueOrDefault(key))
            {
                break;
            }

            // if the result came from what was a literal value then stop expanding
            // BUT only if the literal value contains expansion patterns (${...} or $VAR)
            if (literals.TryGetValue(key, out string? literalValue) && LiteralsRegex().IsMatch(literalValue))
            {
                break;
            }
        }

        return result;
    }

    private static string JsReplacement(string replacement, string matched, string input, int at)
    {
        if (!replacement.Contains((char)36))
        {
            return replacement;
        }

        StringBuilder sb = new();
        for (int i = 0; i < replacement.Length; i++)
        {
            char c = replacement[i];
            if (c != (char)36 || i + 1 >= replacement.Length)
            {
                sb.Append(c);
                continue;
            }

            char n = replacement[i + 1];
            if (n == (char)36)
            {
                sb.Append((char)36);
            }
            else if (n == (char)38)
            {
                sb.Append(matched);
            }
            else if (n == (char)96)
            {
                sb.Append(input, 0, at);
            }
            else if (n == (char)39)
            {
                sb.Append(input, at + matched.Length, input.Length - at - matched.Length);
            }
            else
            {
                sb.Append(c);
                continue;
            }

            i++;
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"\$\{[^}]+\}|\$[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex LiteralsRegex();

    [GeneratedRegex(@"(?<!\\)\${([^{}]+)}|(?<!\\)\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ExpandRegex();

    [GeneratedRegex(@"(:\+|\+|:-|-)")]
    private static partial Regex OpRegex();
}
