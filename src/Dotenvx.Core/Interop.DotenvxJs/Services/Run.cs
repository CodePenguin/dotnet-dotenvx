using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/run.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Run
{
    private static readonly Func<EnvItem[], bool, IDictionary<string, string>?, string?, Run> DefaultFactory =
        (envs, overload, processEnv, envKeysFilePath) => new Run(envs, overload, processEnv, envKeysFilePath);

    private readonly IDictionary<string, string> _beforeEnv;
    private readonly string? _envKeysFilePath;
    private readonly EnvItem[] _envs;
    private readonly bool _opsOn;
    private readonly bool _overload;

    private readonly List<RunProcessedEnv> _processedEnvs = [];
    private readonly IDictionary<string, string> _processEnv;
    private readonly HashSet<string> _readableFilePaths = [];
    private readonly HashSet<string> _readableStrings = [];
    private readonly HashSet<string> _uniqueInjectedKeys = [];
    private readonly bool _useSystemEnv;

    protected Run()
    {
        // For unit tests
        _beforeEnv = new Dictionary<string, string>();
        _envs = [];
        _processedEnvs = [];
        _processEnv = new Dictionary<string, string>();
    }

    public Run(EnvItem[]? envs = null, bool overload = false,
        IDictionary<string, string>? processEnv = null, string? envKeysFilePath = null, bool opsOn = true)
    {
        _useSystemEnv = processEnv == null;
        _processEnv = processEnv ?? EnvironmentHelper.GetEnvironmentVariablesAsDictionary();
        _beforeEnv = new Dictionary<string, string>(_processEnv);
        _envs = DetermineEnvs(envs ?? [], _processEnv);
        _opsOn = opsOn;
        _overload = overload;
        _envKeysFilePath = envKeysFilePath;
    }

    public static Func<EnvItem[], bool, IDictionary<string, string>?, string?, Run>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
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
            ProcessedEnvs = _processedEnvs.ToArray(),
            ReadableStrings = _readableStrings.ToArray(),
            ReadableFilePaths = _readableFilePaths.ToArray(),
            UniqueInjectedKeys = _uniqueInjectedKeys.ToArray(),
            BeforeEnv = _beforeEnv.ToDictionary(),
            AfterEnv = new Dictionary<string, string>(_processEnv)
        };
    }

    private void InjectEnv(string env)
    {
        RunProcessedEnv row = new() { Type = EnvType.Env, String = env };

        try
        {
            ParseResult parseResult = Parse.Factory(env, null, _processEnv, _overload, null).Execute();
            row.Parsed = parseResult.Parsed;
            row.Errors = parseResult.Errors;
            row.Injected = parseResult.Injected;
            row.PreExisted = parseResult.PreExisted;

            Inject(row.Parsed);

            _readableStrings.Add(env);
            foreach (string key in row.Injected.Keys)
            {
                _uniqueInjectedKeys.Add(key); // track uniqueInjectedKeys across multiple files
            }
        }
        catch (Exception e)
        {
            row.Errors.Add(e);
        }

        _processedEnvs.Add(row);
    }

    private void Inject(IReadOnlyDictionary<string, string> parsed)
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

    private void InjectEnvFile(string envFilePath)
    {
        RunProcessedEnv row = new() { Type = EnvType.EnvFile, FilePath = envFilePath };

        string filepath = Path.GetFullPath(envFilePath);

        try
        {
            string encoding = DetectEncoding(filepath);
            string src = Fsx.ReadFileX(filepath, encoding);
            _readableFilePaths.Add(envFilePath);

            string? privateKey = FindPrivateKey(envFilePath, _envKeysFilePath, _opsOn);
            string privateKeyName = GuessPrivateKeyName(envFilePath);

            ParseResult parseResult = Parse.Factory(src, privateKey, _processEnv, _overload, privateKeyName).Execute();

            row.PrivateKeyName = privateKeyName;
            row.PrivateKey = privateKey;
            row.Src = src;
            row.Parsed = parseResult.Parsed;
            row.Errors = parseResult.Errors;
            row.Injected = parseResult.Injected;
            row.PreExisted = parseResult.PreExisted;

            Inject(row.Parsed.ToDictionary());

            foreach (string key in row.Injected.Keys)
            {
                _uniqueInjectedKeys.Add(key);
            }
        }
        catch (Exception e)
        {
            Exception error = e is FileNotFoundException ||
                              (e is UnauthorizedAccessException && Directory.Exists(filepath))
                ? new MissingEnvFileException(envFilePath, filepath)
                : e;
            row.Errors.Add(error);
        }

        _processedEnvs.Add(row);
    }
}