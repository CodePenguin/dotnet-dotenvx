using System.Collections;
using System.Text;

using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public class Run
{
    private readonly IDictionary<string, string> _beforeEnv;
    private readonly string? _envKeysFilepath;
    private readonly List<EnvItem> _envs;
    private readonly bool _overload;

    private readonly List<ProcessedEnv> _processedEnvs = [];
    private readonly IDictionary<string, string> _processEnv;
    private readonly HashSet<string> _readableFilepaths = [];
    private readonly HashSet<string> _readableStrings = [];
    private readonly HashSet<string> _uniqueInjectedKeys = [];
    private readonly bool _useSystemEnv;

    public Run(List<EnvItem>? envs = null, bool overload = false, string? envKeysFilepath = null,
        IDictionary<string, string>? processEnv = null)
    {
        _useSystemEnv = processEnv == null;
        _processEnv = processEnv ?? new Dictionary<string, string>(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        _beforeEnv = new Dictionary<string, string>(_processEnv);
        _envs = Helpers.Helpers.DetermineEnvs(envs ?? [], _processEnv);
        _overload = overload;
        _envKeysFilepath = envKeysFilepath;
    }

    public virtual RunResult Execute()
    {
        foreach (EnvItem env in _envs)
        {
            switch (env.Type)
            {
                case EnvType.EnvFile:
                    InjectEnvFile(env.Value);
                    break;
                case EnvType.Env:
                    InjectEnv(env.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return new RunResult
        {
            ProcessedEnvs = _processedEnvs,
            ReadableStrings = _readableStrings.ToList(),
            ReadableFilepaths = _readableFilepaths.ToList(),
            UniqueInjectedKeys = _uniqueInjectedKeys.ToList(),
            BeforeEnv = _beforeEnv,
            AfterEnv = new Dictionary<string, string>(_processEnv)
        };
    }

    private void InjectEnv(string env)
    {
        ProcessedEnv row = new() { Type = EnvType.Env, String = env };

        try
        {
            ParseResult parseResult = Main.ParseFactory(env, null, _processEnv, _overload, null).Execute();
            row.Parsed = parseResult.Parsed;
            row.Errors = parseResult.Errors;
            row.Injected = parseResult.Injected;
            row.PreExisted = parseResult.PreExisted;

            Inject(row.Parsed);
            _readableStrings.Add(env);
            foreach (string key in row.Injected.Keys)
            {
                _uniqueInjectedKeys.Add(key);
            }
        }
        catch (Exception e)
        {
            row.Errors.Add(e);
        }

        _processedEnvs.Add(row);
    }

    private void Inject(IDictionary<string, string> parsed)
    {
        foreach ((string key, string value) in parsed)
        {
            _processEnv[key] = value;
            if (_useSystemEnv)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private void InjectEnvFile(string envFilepath)
    {
        ProcessedEnv row = new() { Type = EnvType.EnvFile, Filepath = envFilepath };

        string filepath = Path.GetFullPath(envFilepath);

        try
        {
            if (!File.Exists(filepath))
            {
                throw new MissingEnvFileException(envFilepath, filepath);
            }

            Encoding encoding = DetectEncoding.Execute(filepath);
            string src = File.ReadAllText(filepath, encoding);
            _readableFilepaths.Add(envFilepath);

            string? privateKey = Helpers.Helpers.FindPrivateKey(envFilepath, _envKeysFilepath);
            string privateKeyName = Helpers.Helpers.GuessPrivateKeyName(envFilepath);

            ParseResult parseResult =
                Main.ParseFactory(src, privateKey, _processEnv, _overload, privateKeyName).Execute();
            row.PrivateKeyName = privateKeyName;
            row.PrivateKey = privateKey;
            row.Src = src;
            row.Parsed = parseResult.Parsed;
            row.Errors = parseResult.Errors;
            row.Injected = parseResult.Injected;
            row.PreExisted = parseResult.PreExisted;

            Inject(row.Parsed);
            foreach (string key in row.Injected.Keys)
            {
                _uniqueInjectedKeys.Add(key);
            }
        }
        catch (Exception e)
        {
            row.Errors.Add(e);
        }

        _processedEnvs.Add(row);
    }
}