using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/envs.js (moved there from src/lib/services/run.js -
/// confirmed 2026-09-24 that services/run.js no longer exists at all at this pin, part of the same
/// custodian/keyring reorg AGENTS.md already documents for ls.js/keynames.js/environment.js). The
/// current resolver adds 1Password/Bitwarden secret-reference resolution and Armor provider/
/// decryptor support, both confirmed out of scope for this port - the core parse/inject/error-
/// handling logic this class implements (including ENOENT/EISDIR -&gt; MissingEnvFileException,
/// matched here via UnauthorizedAccessException + Directory.Exists) was cross-checked against the
/// current file and remains accurate.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Run
{
    private static readonly Func<EnvItem[], bool, IDictionary<string, string>?, string[]?, Run> DefaultFactory =
        (envs, overload, processEnv, envKeysFilePaths) => new Run(envs, overload, processEnv, envKeysFilePaths);

    private readonly IDictionary<string, string> _beforeEnv;
    private readonly string[]? _envKeysFilePaths;
    private readonly EnvItem[] _envs;

    /// <summary>How missing private keys are looked up: --no-native, --no-armor, --token (providers/index.js options).</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new();

    public bool NoNative
    {
        get => KeyAccess.NoNative;
        set => KeyAccess.NoNative = value;
    }

    /// <summary>--no-1password (or DOTENVX_NO_1PASSWORD=true): leave `op://` references unresolved.</summary>
    public bool No1Password { get; set; }

    /// <summary>--no-bitwarden (or DOTENVX_NO_BITWARDEN=true): leave `bw://` references unresolved.</summary>
    public bool NoBitwarden { get; set; }

    /// <summary>The async resolver variant (run/get/validate): may prompt for the Bitwarden master password on a TTY.</summary>
    public bool PromptForSecrets { get; set; }

    // main.js config() only runs `determine` when no explicit `envs` option was given; every other caller determines
    public bool DetermineOnExecute { get; set; } = true;
    private readonly bool _opsOn;
    private readonly bool _overload;

    private readonly List<RunProcessedEnv> _processedEnvs = [];
    private readonly IDictionary<string, string> _processEnv;
    private readonly HashSet<string> _readableFilePaths = [];
    private readonly HashSet<string> _readableStrings = [];
    private readonly HashSet<string> _uniqueInjectedKeys = [];
    private readonly bool _useSystemEnv;

    private Func<string, IDictionary<string, string>?>? _provider;
    private Decryptor? _decryptor;

    protected Run()
    {
        // For unit tests
        _beforeEnv = new Dictionary<string, string>();
        _envs = [];
        _processedEnvs = [];
        _processEnv = new Dictionary<string, string>();
    }

    public Run(EnvItem[]? envs = null, bool overload = false,
        IDictionary<string, string>? processEnv = null, string[]? envKeysFilePaths = null, bool opsOn = true)
    {
        _useSystemEnv = processEnv == null;
        _processEnv = processEnv ?? EnvironmentHelper.GetEnvironmentVariablesAsDictionary();
        _beforeEnv = new Dictionary<string, string>(_processEnv);
        _envs = envs ?? [];
        _opsOn = opsOn;
        _overload = overload;
        _envKeysFilePaths = envKeysFilePaths;
    }

    public static Func<EnvItem[], bool, IDictionary<string, string>?, string[]?, Run>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual RunResult Execute()
    {
        _provider = Providers.Resolve(KeyAccess);
        _decryptor = Providers.ResolveDecryptor(KeyAccess);

        EnvItem[] envs = DetermineOnExecute ? DetermineEnvs(_envs, _processEnv) : _envs;
        foreach (EnvItem env in envs)
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
            // envs.js's injectEnv parses with `fk: envKeysFilepath` (null means primitives' own default,
            // `.env.keys` in the working directory) and the shared process environment, which already
            // carries any exported DOTENV_PRIVATE_KEY.
            ParseWithDecryptor.Outcome<PrimitivesParseOutput> outcome = ParseWithDecryptor.Sync(env, new PrimitivesParseOptions
            {
                Overload = _overload,
                ProcessEnv = new Dictionary<string, string>(_processEnv),
                Fk = _envKeysFilePaths is { Length: > 0 } ? _envKeysFilePaths : null,
                Provider = _provider
            }, _decryptor);
            PrimitivesParseOutput parsed = outcome.Output;
            row.Parsed = parsed.Parsed;
            row.Errors = DecryptErrors(parsed, outcome.LeadingError);
            row.Injected = parsed.Injected;
            row.PreExisted = parsed.Existed;
            ResolveSecrets(row);

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

    /// <remarks>
    /// envs.js: after parsing, `if (!no1Password)` / `if (!noBitwarden)` resolve secret references inside
    /// `row.injected`, append their errors to `row.errors` and `Object.assign(row.parsed, row.injected)`. The
    /// flags are `options.noX === true || process.env.DOTENVX_NO_X === 'true'` (the real process environment).
    /// </remarks>
    private void ResolveSecrets(RunProcessedEnv row)
    {
        bool no1Password = No1Password || Environment.GetEnvironmentVariable("DOTENVX_NO_1PASSWORD") == "true";
        bool noBitwarden = NoBitwarden || Environment.GetEnvironmentVariable("DOTENVX_NO_BITWARDEN") == "true";
        if (no1Password && noBitwarden)
        {
            return;
        }

        var injected = new Dictionary<string, string>(row.Injected);
        var parsed = new Dictionary<string, string>(row.Parsed);

        if (!no1Password)
        {
            AddErrors(row, ResolveOnePassword.Resolve(injected).Errors);
            Overlay(parsed, injected);
        }

        if (!noBitwarden)
        {
            AddErrors(row, ResolveBitwardenPassword.Resolve(injected, PromptForSecrets).Errors);
            Overlay(parsed, injected);
        }

        row.Injected = injected;
        row.Parsed = parsed;
    }

    private static void AddErrors(RunProcessedEnv row, IEnumerable<Exception> errors)
    {
        foreach (Exception error in errors)
        {
            row.Errors.Add(error);
        }
    }

    private static void Overlay(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach ((string key, string value) in source)
        {
            target[key] = value;
        }
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

            // envs.js: fk = envKeysFilepath || (processEnv[privateKeyName] ? null : <env dir>/.env.keys);
            // a null fk still means primitives' default `.env.keys` in the working directory.
            string privateKeyName = GuessPrivateKeyName(envFilePath);
            string[]? fk = _envKeysFilePaths is { Length: > 0 } ? _envKeysFilePaths : null;
            if (fk is null && !(_processEnv.TryGetValue(privateKeyName, out string? exported) &&
                                              !string.IsNullOrEmpty(exported)))
            {
                fk = [Path.Combine(Path.GetDirectoryName(filepath) ?? "", ".env.keys")];
            }

            ParseWithDecryptor.Outcome<PrimitivesParseOutput> outcome = ParseWithDecryptor.Sync(src, new PrimitivesParseOptions
            {
                Overload = _overload,
                ProcessEnv = _processEnv,
                Fk = fk,
                Provider = _provider
            }, _decryptor);
            PrimitivesParseOutput parsed = outcome.Output;

            row.Src = src;
            row.Parsed = parsed.Parsed;
            row.Errors = DecryptErrors(parsed, outcome.LeadingError);
            row.Injected = parsed.Injected;
            row.PreExisted = parsed.Existed;
            ResolveSecrets(row);

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
                ? new MissingEnvFileException(envFilePath)
                : e;
            row.Errors.Add(error);
        }

        _processedEnvs.Add(row);
    }

    /// <remarks>
    /// envs.js's `decryptErrors(parsed, errors)`: errors reported by primitives' `parse()` become custom
    /// errors with their own code/message (help is the generic `fix: [issue url]` for the code); when
    /// there are none, any value still encrypted is reported as one combined DECRYPTION_FAILED
    /// ("could not decrypt A, B"). Primitives always coarsens decrypt failures this way, so `run`/`get`/
    /// `validate`/`config` never surface WRONG_PRIVATE_KEY or MISSING_PRIVATE_KEY.
    /// </remarks>
    private static IList<Exception> DecryptErrors(PrimitivesParseOutput parsed, Exception? leadingError)
    {
        // failedKeyAccessFallback: `errors: [error, ...result.errors]`
        List<Exception> mapped = [.. leadingError is null ? [] : new[] { leadingError }, .. parsed.Errors.Select(CustomError)];
        if (mapped.Count > 0)
        {
            return mapped;
        }

        string[] keys = parsed.Parsed.Where(kv => Encrypted(kv.Value)).Select(kv => kv.Key).ToArray();
        return keys.Length == 0
            ? []
            : [new DecryptionFailedException($"could not decrypt {string.Join(", ", keys)}")];
    }
}
