using System.Text.RegularExpressions;

using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public partial class Parse(
    string? src,
    string? privateKey = null,
    IDictionary<string, string>? processEnv = null,
    bool overload = false,
    string? privateKeyName = null)
{
    private readonly List<Exception> _errors = [];
    private readonly IDictionary<string, string> _injected = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _literals = new();
    private readonly Dictionary<string, string> _parsed = new();
    private readonly Dictionary<string, string> _preExisted = new();
    private readonly string _privateKeyName = privateKeyName ?? string.Empty;
    private readonly IDictionary<string, string> _processEnv = processEnv ?? new Dictionary<string, string>();
    private readonly Dictionary<string, string> _runningParsed = new();

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
                catch (Exception e)
                {
                    _errors.Add(e);
                }

                if (priorEvaled != _parsed[key])
                {
                    evaled = true;
                }
            }

            // expand empty, double, or backticks
            if (!evaled && quote != '\'' && (!_processEnv.ContainsKey(key) || overload))
            {
                _parsed[key] = Helpers.ResolveEscapeSequences(Expand(_parsed[key]));
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
            ProcessEnv = _processEnv,
            Injected = _injected,
            PreExisted = _preExisted,
            Errors = _errors
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
        return Helpers.DecryptKeyValue(key, value, _privateKeyName, privateKey) ?? string.Empty;
    }

    private string Eval(string key, string value)
    {
        return Helpers.EvalKeyValue(key, value, _processEnv, _runningParsed);
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

    [GeneratedRegex(
        """(?:^|^)\s*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*"(?:\\"|[^"])*"|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#.*)?(?:$|$)""",
        RegexOptions.Multiline)]
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