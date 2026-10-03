using System.Text.RegularExpressions;

using Dotenvx.Primitives;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: @dotenvx/primitives parseSync (no readable source; checked live by PrimitivesOracleParityTest). Line parsing is the shared Primitives.Scan.</remarks>
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
        foreach (ScanEntry entry in Scan(src))
        {
            string key = entry.Key;
            char quote = entry.Quote;
            _parsed[key] = entry.Value; // file value

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
                _parsed[key] = ResolveEscapeSequences(Expand(_parsed[key], overload, _processEnv, _runningParsed, _literals));
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

    private string Decrypt(string key, string value)
    {
        return DecryptKeyValue(key, value, _privateKeyName, privateKey);
    }

    private string Eval(string key, string value)
    {
        return Evaluate(key, value, _processEnv, _runningParsed);
    }
}