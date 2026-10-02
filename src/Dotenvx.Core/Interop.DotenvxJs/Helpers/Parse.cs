using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/parse.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public partial class Parse(
    string? src,
    string? privateKey = null,
    IDictionary<string, string>? processEnv = null,
    bool overload = false,
    string? privateKeyName = null)
{
    private static readonly Func<string?, string?, IDictionary<string, string>?, bool, string?, Parse> DefaultFactory =
        (src, privateKey, processEnv, overload, privateKeyName) =>
            new Parse(src, privateKey, processEnv, overload, privateKeyName);

    private readonly List<Exception> _errors = [];
    private readonly IDictionary<string, string> _injected = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _literals = new();
    private readonly Dictionary<string, string> _parsed = new();
    private readonly Dictionary<string, string> _preExisted = new();
    private readonly string _privateKeyName = privateKeyName ?? string.Empty;
    private readonly IDictionary<string, string> _processEnv = processEnv ?? new Dictionary<string, string>();
    private readonly Dictionary<string, string> _runningParsed = new();

    public static Func<string?, string?, IDictionary<string, string>?, bool, string?, Parse>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual ParseResult Execute()
    {
        string lines = GetLines();
        MatchCollection matches = LineRegex().Matches(lines);

        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value;
            char quote = Quote(value); // must be raw match
            _parsed[key] = Clean(value, quote); // file value

            if (!overload && _processEnv.TryGetValue(key, out string? processEnvValue))
            {
                _parsed[key] = processEnvValue; // use process.env pre-existing value
            }

            // decrypt
            try
            {
                _parsed[key] = Decrypt(key, _parsed[key]);
            }
            catch (Exception e)
            {
                _errors.Add(e);
            }

            // eval empty, double, or backticks
            bool evaled = false;
            if (quote != '\'' &&
                (!_processEnv.TryGetValue(key, out processEnvValue) || processEnvValue == _parsed[key]))
            {
                string priorEvaled = _parsed[key];
                // eval
                try
                {
                    _parsed[key] = Eval(key, priorEvaled);
                }
                catch (CommandSubstitutionFailed)
                {
                    // Silently leave the value unresolved - matches current dotenvx behavior
                    // (@dotenvx/primitives' parse() swallows this specific error rather than
                    // surfacing it, unlike other parse-time errors such as decryption failures).
                }

                if (priorEvaled != _parsed[key])
                {
                    evaled = true;
                }
            }

            // expand empty, double, or backticks
            if (!evaled && quote != '\'' && (!_processEnv.ContainsKey(key) || overload))
            {
                _parsed[key] = ResolveEscapeSequences(Expand(_parsed[key]));
            }

            if (quote == '\'')
            {
                _literals[key] = _parsed[key];
            }

            // for use with progressive expansion
            _runningParsed[key] = _parsed[key];

            if (_processEnv.TryGetValue(key, out processEnvValue) && !overload)
            {
                _preExisted[key] = processEnvValue; // track preExisted
            }
            else
            {
                _injected[key] = _parsed[key]; // track injected
            }
        }

        return new ParseResult
        {
            Parsed = _parsed,
            ProcessEnv = _processEnv.ToDictionary(),
            Injected = _injected.ToDictionary(),
            PreExisted = _preExisted,
            Errors = _errors.ToArray()
        };
    }

    private static string Trimmer(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static char Quote(string value)
    {
        if (string.IsNullOrEmpty(Trimmer(value)))
        {
            return ' ';
        }

        char maybeQuote = value[0];
        return maybeQuote switch
        {
            '\'' => '\'',
            '"' => '"',
            '`' => '`',
            _ => ' '
        };
    }

    private static string Clean(string value, char quote)
    {
        // Remove surrounding quotes
        value = CleanRegex().Replace(Trimmer(value), "$2");

        // Unescape \' if single quoted (primitives' value pass)
        if (quote == '\'')
        {
            value = value.Replace("\\'", "'");
        }

        // Expand newlines if double quoted
        if (quote == '"')
        {
            value = value
                .Replace("\\n", "\n") // newline
                .Replace("\\r", "\r") // carriage return
                .Replace("\\t", "\t"); // tabs
        }

        return value;
    }

    private string Decrypt(string key, string value)
    {
        return DecryptKeyValue(key, value, _privateKeyName, privateKey);
    }

    private string Eval(string key, string value)
    {
        return Evaluate(key, value, _processEnv, _runningParsed);
    }

    private string Expand(string value)
    {
        Dictionary<string, string> env;
        if (overload)
        {
            env = new Dictionary<string, string>(_processEnv);
            _runningParsed.ToList().ForEach(p => env[p.Key] = p.Value);
        }
        else
        {
            env = new Dictionary<string, string>(_runningParsed);
            _processEnv.ToList().ForEach(p => env[p.Key] = p.Value);
        }

        string result = value;
        Match match;

        while ((match = ExpandRegex().Match(result)).Success)
        {
            string template = match.Groups[0].Value;
            string expression = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            // match the operators `:+`, `+`, `:-`, and `-`
            // find first match
            Match opMatch = OpRegex().Match(expression);
            string? splitter = opMatch.Success ? opMatch.Value : null;

            string[] parts = splitter != null ? expression.Split([splitter], StringSplitOptions.None) : [expression];

            string defaultValue;
            string? val;
            string key = parts[0];

            if (splitter is ":+" or "+")
            {
                defaultValue = env.ContainsKey(key) ? string.Join(splitter, parts, 1, parts.Length - 1) : "";
                val = null;
            }
            else
            {
                defaultValue = string.Join(splitter, parts, 1, parts.Length - 1);
                val = env.GetValueOrDefault(key);
            }

            result = result.Replace(template, val ?? defaultValue);

            // if the result equaled what was in env then stop expanding - handle self-referential check as well
            if (result == env.GetValueOrDefault(key))
            {
                break;
            }

            // if the result came from what was a literal value then stop expanding
            // BUT only if the literal value contains expansion patterns (${...} or $VAR)
            if (_literals.TryGetValue(key, out string? literalValue) && LiteralsRegex().IsMatch(literalValue))
            {
                break;
            }
        }

        return result;
    }

    private string GetLines()
    {
        return NormalizeLineEndingsRegex().Replace(src ?? string.Empty, "\n");
    }

    [GeneratedRegex(@"^(['""`])([\s\S]*)\1$")]
    private static partial Regex CleanRegex();

    // Leading [\s\uFEFF]* (not just \s*) so a byte-order-mark surviving at the very start of the
    // decoded source (see Fsx.ReadFileX's BOM-preservation trap in CLAUDE.md) doesn't stop the
    // FIRST key from being recognized - JS's \s regex metaclass matches U+FEFF; .NET's does not.
    // RegexOptions.ECMAScript makes \w ASCII-only (matching JS's \w exactly) instead of .NET's
    // default Unicode-aware \w.
    [GeneratedRegex(
        """(?:^|^)[\s\uFEFF]*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*"(?:\\"|[^"])*"|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)""",
        RegexOptions.Multiline | RegexOptions.ECMAScript)]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"\$\{[^}]+\}|\$[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex LiteralsRegex();

    [GeneratedRegex(@"(?<!\\)\${([^{}]+)}|(?<!\\)\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ExpandRegex();

    [GeneratedRegex(@"(:\+|\+|:-|-)")]
    private static partial Regex OpRegex();

    [GeneratedRegex(@"\r\n?|\r")]
    private static partial Regex NormalizeLineEndingsRegex();
}