using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/envs.js (moved there from src/lib/services/run.js -
/// confirmed 2026-09-24 that services/run.js no longer exists at all at this pin, part of the same
/// custodian/keyring reorg CLAUDE.md already documents for ls.js/keynames.js/environment.js). The
/// current resolver adds 1Password/Bitwarden secret-reference resolution and Armor provider/
/// decryptor support, both confirmed out of scope for this port - the core parse/inject/error-
/// handling logic this class implements (including ENOENT/EISDIR -&gt; MissingEnvFileException,
/// matched here via UnauthorizedAccessException + Directory.Exists) was cross-checked against the
/// current file and remains accurate.
/// </remarks>
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
            // envs.js's real injectEnv forwards the process environment's own DOTENV_PRIVATE_KEY
            // into the parse call (a raw --env string has no filename, so there's no convention-
            // specific *_PRODUCTION-style suffix to derive - DOTENV_PRIVATE_KEY is the only
            // candidate). Confirmed live: `DOTENV_PRIVATE_KEY=... dotenvx run --env "KEY=encrypted:
            // ..."` decrypts successfully on real Node with no .env file involved at all - this
            // port previously hardcoded privateKey: null here, so the exact same scenario always
            // failed with DECRYPTION_FAILED regardless of what was exported in the shell.
            _processEnv.TryGetValue("DOTENV_PRIVATE_KEY", out string? privateKey);
            ParseResult parseResult =
                Parse.Factory(env, privateKey, _processEnv, _overload, "DOTENV_PRIVATE_KEY").Execute();
            row.Parsed = parseResult.Parsed;
            row.Errors = CoarsenDecryptErrors(parseResult.Errors);
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
            row.Errors = CoarsenDecryptErrors(parseResult.Errors);
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
                ? new MissingEnvFileException(envFilePath)
                : e;
            row.Errors.Add(error);
        }

        _processedEnvs.Add(row);
    }

    /// <remarks>
    /// Parse.cs's per-key DecryptKeyValue call (see DecryptKeyValues.cs) always throws the SPECIFIC
    /// failure cause (WrongPrivateKeyException/MissingPrivateKeyException/InvalidPrivateKeyException/
    /// MalformedEncryptedDataException) - correct for Main.cs's Parse() method, which mirrors
    /// main.js's own standalone `parse()` function (its own manual per-key decryptKeyValue() loop,
    /// confirmed still true at this pin). But Run.cs itself mirrors resolvers/envs.js, which - like
    /// every other resolver (get.js, and main.js's own config()/get() functions, both of which route
    /// through the SAME resolver) - decrypts via @dotenvx/primitives' own internal `parse()`, which
    /// ALWAYS coarsens any decrypt failure to a generic DECRYPTION_FAILED ("could not decrypt {key}")
    /// regardless of cause - confirmed live 2026-09-25 by probing parse() directly with both a wrong-
    /// but-valid-format key and a fully missing key, both producing DECRYPTION_FAILED, never
    /// WRONG_PRIVATE_KEY/MISSING_PRIVATE_KEY. Since Get.cs/ValidateAction.cs/Main.Config()/Main.Get()
    /// all resolve through THIS class's Execute(), fixing it here fixes all of them at once.
    /// </remarks>
    private static IList<Exception> CoarsenDecryptErrors(IReadOnlyList<Exception> errors)
    {
        // Confirmed live by calling @dotenvx/primitives' parse() directly with two simultaneously
        // undecryptable keys: it returns ONE error whose message lists every failing key,
        // comma-joined ("could not decrypt FOO, BAR"), not one discrete error per key - matched via
        // the real CLI too (`dotenvx get` on a file with two bad encrypted values prints exactly one
        // combined DECRYPTION_FAILED line on Node, but this port previously printed two separate
        // lines, one per key).
        List<string> decryptFailedKeys = [];
        List<Exception> otherErrors = [];

        foreach (Exception error in errors)
        {
            string? key = error switch
            {
                WrongPrivateKeyException e => e.Key,
                MissingPrivateKeyException e => e.Key,
                InvalidPrivateKeyException e => e.Key,
                MalformedEncryptedDataException e => e.Key,
                _ => null
            };

            if (key != null)
            {
                decryptFailedKeys.Add(key);
            }
            else
            {
                otherErrors.Add(error);
            }
        }

        if (decryptFailedKeys.Count == 0)
        {
            return otherErrors;
        }

        otherErrors.Add(new DecryptionFailedException($"could not decrypt {string.Join(", ", decryptFailedKeys)}"));
        return otherErrors;
    }
}