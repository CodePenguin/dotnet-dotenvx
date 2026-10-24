using System.Collections;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/src/lib/main.js</remarks>
internal static class Main
{
    public static ConfigResult Config(ConfigOptions? options = null)
    {
        return Config(options, null);
    }

    /// <summary>Plain `config` or the instrumented one behind <see cref="WithEvents" />.</summary>
    internal static ConfigResult Config(ConfigOptions? options, EventRecorder? events)
    {
        options = WithLogger(options ?? new ConfigOptions());
        options = NormalizeDotenvConfigQuiet(options);
        options = NormalizeDotenvConfigConvention(options);
        options = NormalizeDotenvConfigIgnore(options);

        IDotenvxLogger? logger = options.Logger;
        SetLogLevel(logger, options);

        // allow user to set processEnv to write to
        bool useSystemEnv = options.ProcessEnv == null;
        IDictionary<string, string> processEnv =
            options.ProcessEnv ??
            new Dictionary<string, string>(global::Dotenvx.Helpers.EnvironmentHelper.GetEnvironmentVariablesAsDictionary());

        // ignore
        List<string> ignore = options.Ignore ?? [];

        // strict
        bool strict = options.Strict ?? false;

        // envKeysFile
        string[]? envKeysFile = string.IsNullOrEmpty(options.EnvKeysFile) ? null : [options.EnvKeysFile];

        // dotenvx-armor related (asked before anything else, as main.js does)
        bool noArmor = ResolveNoArmor(options);

        bool fatal = false;
        string[] sensitiveValues = [];
        try
        {
            // `{ ...process.env, ...processEnv }`
            Dictionary<string, string?> selectionEnv = new(Helpers.Helpers.CurrentProcessEnv());
            foreach ((string name, string value) in processEnv)
            {
                selectionEnv[name] = value;
            }

            EnvItem[] envs = options.Envs ?? SelectEnvfileEnvs(BuildConfigEnvs(options),
                new SelectEnvfileOptions { Profile = options.Profile }, selectionEnv);
            EnvfileSchema schema = ReadEnvfile.Read(null, envs.Where(env => env.Type == EnvType.EnvFile).Select(env => env.Value));

            Run run = Run.Factory(envs, options.Overload == true || options.Override == true, processEnv, envKeysFile);
            run.DetermineOnExecute = false;
            run.NoNative = options.NoNative == true || options.Native == false;
            run.KeyAccess.NoArmor = noArmor;
            run.KeyAccess.Logger = options.Logger;
            run.KeyAccess.Token = options.Token;
            run.No1Password = options.No1Password == true;
            run.NoBitwarden = options.NoBitwarden == true;
            RunResult runResult = run.Execute();

            CheckResult check = schema.Exists
                ? global::Dotenvx.Primitives.Primitives.Check(schema.Policy, new Dictionary<string, string>(processEnv))
                : new CheckResult();
            InvalidEnvException? validationError = PrepareValidatedEnv.ValidationError(check.Errors);
            sensitiveValues = RedactedValues(runResult.ProcessedEnvs, schema, processEnv);
            if (schema.Exists)
            {
                RedactProcessOutput.Install(sensitiveValues);
            }

            if (options.Mask != null)
            {
                object showChar = Equals(options.Mask, true) ? 6 : options.Mask;
                MaskProcessedEnvs(runResult.ProcessedEnvs, processEnv, showChar);
            }

            Exception? lastError = null;
            Dictionary<string, string> parsedAll = [];

            foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
            {
                if (processedEnv.Type == EnvType.EnvFile)
                {
                    logger?.Verbose(
                        $"loading env from {processedEnv.FilePath} ({NodePath.Resolve(processedEnv.FilePath)})");
                }

                foreach (Exception error in processedEnv.Errors)
                {
                    string errorCode = ErrorCodeOf(error) ?? "GENERIC_ERROR";

                    // Check if the error code is in the ignore list
                    bool isIgnored = ignore.Contains(errorCode); // exact code match, like `ignore.includes(error.code)`
                    if (isIgnored)
                    {
                        logger?.Verbose($"ignored: {error.Message}");
                        continue;
                    }

                    if (strict)
                    {
                        fatal = true;

                        throw error; // Re-throw the original exception if strict mode is enabled
                    }

                    lastError = error;

                    if (errorCode == "MISSING_ENV_FILE" && !string.IsNullOrEmpty(options.Convention))
                    {
                        continue;
                    }

                    logger?.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
                }

                // debug parsed
                // a file that could not be loaded has no `parsed` (JS prints `undefined`)
                logger?.Debug(processedEnv.Errors.Any(failure => ErrorCodeOf(failure) == "MISSING_ENV_FILE")
                    ? "undefined"
                    : RedactOutputText(JsonStringify(processedEnv.Parsed), sensitiveValues)!);

                foreach (KeyValuePair<string, string> entry in processedEnv.Injected)
                {
                    parsedAll[entry.Key] = entry.Value;

                    // verbose/debug injected key/value
                    logger?.Verbose($"{entry.Key} set");
                    logger?.Debug(RedactOutputText($"{entry.Key} set to {entry.Value}", sensitiveValues)!);

                    // Inject into System Environment
                    if (useSystemEnv)
                    {
                        Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                    }
                }

                foreach (KeyValuePair<string, string> entry in processedEnv.PreExisted)
                {
                    parsedAll[entry.Key] = entry.Value;
                    // verbose/debug preExisted key/value
                    logger?.Verbose($"{entry.Key} pre-exists (protip: use --overload to override)");
                    logger?.Debug(RedactOutputText(
                        $"{entry.Key} pre-exists as {entry.Value} (protip: use --overload to override)", sensitiveValues)!);
                }
            }

            if (validationError is not null)
            {
                if (ignore.Contains(validationError.Code))
                {
                    logger?.Verbose($"ignored: {validationError.Message}");
                }
                else if (strict || validationError.Diagnostics.Any(diagnostic => diagnostic.Strict))
                {
                    fatal = true;

                    throw validationError;
                }
                else
                {
                    logger?.Warn(validationError.MessageWithHelp ?? validationError.Message);
                    lastError = validationError;
                }
            }

            events?.Add(new Dictionary<string, object?>
            {
                ["files"] = runResult.ReadableFilePaths,
                ["injected_count"] = (long)runResult.UniqueInjectedKeys.Length
            });
            logger?.Success(global::Dotenvx.Primitives.Primitives.InjectSummary(runResult.UniqueInjectedKeys.Length,
                runResult.ReadableFilePaths.Select(filepath =>
                {
                    RunProcessedEnv? row = runResult.ProcessedEnvs.FirstOrDefault(item => item.FilePath == filepath);

                    return new InjectSummaryFile(filepath,
                        row is null ? [] : global::Dotenvx.Primitives.Primitives.PublicKeys(row.Src), row?.KeySources);
                })));

            if (lastError != null)
            {
                events?.Fail(lastError);
            }

            return lastError != null
                ? new ConfigResult { Parsed = parsedAll, Error = lastError }
                : new ConfigResult { Parsed = parsedAll }; // No error
        }
        catch (Exception error)
        {
            events?.Fail(error);
            if (strict || fatal || ErrorCodeOf(error) == "MALFORMED_ENVFILE")
            {
                throw; // re-throw if strict
            }

            logger?.Error(RedactOutputText((error as DotenvxException)?.MessageWithHelp ?? error.Message, sensitiveValues)!);

            return new ConfigResult { Parsed = new Dictionary<string, string>(), Error = error };
        }
    }

    /// <summary>`resolveNoArmor(options)`: `noArmor`, or (without a token) no active Armor session.</summary>
    private static bool ResolveNoArmor(ConfigOptions? options)
    {
        return options?.NoArmor == true || (string.IsNullOrEmpty(options?.Token) && new Db.Session(options?.Logger).NoArmor());
    }

    /// <summary>Without a logger of its own an options object logs through <see cref="global::Dotenvx.Dotenvx.Logger" />, the way main.js always uses `logger`.</summary>
    private static T WithLogger<T>(T options) where T : ConfigOptions
    {
        if (options.Logger != null)
        {
            return options;
        }

        var copy = (T)options.Clone();
        copy.Logger = global::Dotenvx.Dotenvx.Logger;

        return copy;
    }

    internal static void SetLogLevel(IDotenvxLogger? logger, ConfigOptions options)
    {
        if (logger == null)
        {
            return;
        }

        // setLogLevel (shared/logger.js): debug, then verbose, then quiet (error), else `options.logLevel`; nothing at all when none is given
        string? name = options.Debug == true ? "debug"
            : options.Verbose == true ? "verbose"
            : options.Quiet == true ? "error"
            : options.LogLevel;
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        DotenvxLogLevel logLevel = DotenvxLogLevelExtensions.FromString(name);
        logger.SetLevel(logLevel);
        // Only log which level it's setting if it's not set to quiet mode
        if (options.Quiet != true || name != "error")
        {
            logger.Debug($"setting log level to: {name}");
        }
    }

    public static object? Get(string? key, GetOptions? options = null)
    {
        return Get(key, options, null);
    }

    /// <summary>
    /// `getSync(key, options)`: the same as <see cref="Get(string?, GetOptions?)" /> without anything interactive (no prompt, no
    /// Bitwarden unlock) and without an SDK event.
    /// </summary>
    public static object? GetSync(string? key, GetOptions? options = null)
    {
        return Get(key, options, null, false);
    }

    internal static object? Get(string? key, GetOptions? options, EventRecorder? events)
    {
        return Get(key, options, events, true);
    }

    private static object? Get(string? key, GetOptions? options, EventRecorder? events, bool promptForSecrets)
    {
        options = WithLogger(options ?? new GetOptions());
        options = (GetOptions)NormalizeDotenvConfigConvention(options);
        options = (GetOptions)NormalizeDotenvConfigIgnore(options);

        EnvItem[] envs = BuildEnvs(options);
        Services.Get getService = Services.Get.Factory(key, envs, options.Overload ?? false, options.All ?? false,
            string.IsNullOrEmpty(options.EnvKeysFile) ? null : [options.EnvKeysFile]);
        getService.NoNative = options.NoNative == true || options.Native == false;
        getService.KeyAccess.NoArmor = ResolveNoArmor(options);
        getService.KeyAccess.Logger = options.Logger;
        getService.No1Password = options.No1Password == true;
        getService.KeyAccess.No1Password = getService.No1Password;
        getService.KeyAccess.NoBitwarden = options.NoBitwarden == true;
        getService.NoBitwarden = options.NoBitwarden == true;
        getService.Profile = options.Profile;
        getService.ResolveDirectories = false;
        getService.PromptForSecrets = promptForSecrets;
        GetResult result = getService.Run();

        IReadOnlyDictionary<string, string> parsed = result.Parsed;
        if (options.Mask != null)
        {
            object showChar = Equals(options.Mask, true) ? 6 : options.Mask;
            parsed = parsed.ToDictionary(kv => kv.Key, kv => Mask(kv.Value, showChar));
        }

        // handle errors
        List<string> ignore = options.Ignore ?? [];
        foreach (Exception error in result.Errors)
        {
            string errorCode = error is DotenvxException dotenvxError ? dotenvxError.Code : "GENERIC_ERROR";
            if (ignore.Contains(errorCode))
            {
                continue;
            }

            events?.Fail(error);
            if (options.Strict == true)
            {
                throw error;
            }

            options.Logger?.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
        }

        events?.Add(new Dictionary<string, object?>
        {
            ["error_count"] = (long)result.Errors.Count(error =>
                !ignore.Contains(error is DotenvxException dotenvx ? dotenvx.Code : "GENERIC_ERROR"))
        });

        if (key != null)
        {
            return parsed.GetValueOrDefault(key);
        }

        // If Format is specified, return a formatted string
        if (!string.IsNullOrEmpty(options.Format))
        {
            return options.Format switch
            {
                "eval" or "eval-export" => string.Join('\n', parsed.Select(kv =>
                    $"{(options.Format == "eval-export" ? "export " : "")}{kv.Key}={Escape(kv.Value)}")),
                "shell" => string.Join(' ', parsed.Select(kv => $"{kv.Key}={kv.Value}")),
                "colon" => string.Join(' ', parsed.Select(kv => $"{kv.Key}:{kv.Value}")),
                _ => parsed
            };
        }

        // Otherwise return the dictionary
        return parsed;
    }

    public static string[] Ls(string directory = "./", string[]? envFile = null, string[]? excludeEnvFile = null)
    {
        return Services.Ls.Factory(directory, envFile, excludeEnvFile).Run();
    }

    public static IReadOnlyDictionary<string, string> KeyPair(string envFile = ".env", string? key = null,
        string? envKeysFile = null)
    {
        Dictionary<string, string?> keypairs =
            Services.KeyPair.Factory(envFile, envKeysFile is null ? null : [envKeysFile]).Run();
        return !string.IsNullOrEmpty(key)
            ? new Dictionary<string, string> { { key, keypairs.GetValueOrDefault(key) ?? "" } }
            : keypairs.ToDictionary(kv => kv.Key, kv => kv.Value ?? "");
    }

    public static IReadOnlyDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        options ??= new ParseOptions();
        IDotenvxLogger? logger = options.Logger ?? global::Dotenvx.Dotenvx.Logger;
        Dictionary<string, string> processEnv = options.ProcessEnv?.ToDictionary() ??
                                                new Dictionary<string, string>(global::Dotenvx.Helpers.EnvironmentHelper.GetEnvironmentVariablesAsDictionary());

        // private decryption key
        string? privateKey = string.IsNullOrEmpty(options.PrivateKey) ? null : options.PrivateKey;

        // overload
        bool overload = options.Overload == true || options.Override == true;

        // ignore
        string[] ignore = options.Ignore ?? [];

        if (privateKey != null)
        {
            processEnv["DOTENV_PRIVATE_KEY"] = privateKey;
        }

        // main.js: primitives' parseSync first (default keyring), then a manual per-key decryptKeyValue
        // pass over whatever is still encrypted, which is why this function (unlike run/get/validate)
        // surfaces the specific WRONG_PRIVATE_KEY/MISSING_PRIVATE_KEY codes.
        Dictionary<string, string> parsed = new(ParseSync(src,
            new PrimitivesParseOptions { ProcessEnv = processEnv, Overload = overload }).Parsed);
        List<Exception> errors = [];

        foreach (string key in parsed.Keys.ToList())
        {
            if (!Encrypted(parsed[key]))
            {
                continue;
            }

            if (privateKey == null)
            {
                errors.Add(new MissingPrivateKeyException(key, "DOTENV_PRIVATE_KEY", ""));
                continue;
            }

            try
            {
                parsed[key] = DecryptKeyValue(key, parsed[key], "DOTENV_PRIVATE_KEY", privateKey);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        // display any errors
        foreach (Exception error in errors)
        {
            if (error is DotenvxException { Code: var code } && ignore.Contains(code))
            {
                logger?.Verbose($"ignored: {error.Message}");
                continue; // ignore error
            }

            logger?.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
        }

        return parsed;
    }

    /// <summary>
    /// An explicitly instrumented SDK client using connected Armor (main.js `withEvents`). `Config` stays synchronous;
    /// `Get`/`Set` preserve their results and errors. Plain SDK calls are unchanged.
    /// </summary>
    public static EventSdk WithEvents()
    {
        return new EventSdk();
    }

    public static SetsResult Set(string key, string value, SetsOptions? options = null)
    {
        return Set(key, value, options, null);
    }

    internal static SetsResult Set(string key, string value, SetsOptions? options, EventRecorder? events)
    {
        options = WithLogger(options ?? new SetsOptions());
        options = (SetsOptions)NormalizeDotenvConfigQuiet(options);

        // encrypt
        bool encrypt = !(options?.Plain == true || options?.Encrypt == false);
        bool noCreate = options?.NoCreate == true;
        string withEncryption = encrypt ? " with encryption" : "";

        if (options != null)
        {
            SetLogLevel(options.Logger, options);
        }

        EnvItem[] envs = BuildEnvs(options ?? new SetsOptions());
        string? envKeysFilepath = options?.EnvKeysFile;

        Sets setsService = Sets.Factory(key, value, envs, encrypt, envKeysFilepath, noCreate);
        setsService.NoNative = options?.NoNative == true || options?.Native == false;
        setsService.KeyAccess.NoArmor = ResolveNoArmor(options);
        setsService.KeyAccess.Logger = options?.Logger;
        setsService.KeyAccess.No1Password = options?.No1Password == true;
        setsService.KeyAccess.NoBitwarden = options?.NoBitwarden == true;
        setsService.Profile = options?.Profile;
        SetsResult result = setsService.Run();

        if (!string.IsNullOrEmpty(result.KeysSrc))
        {
            Fsx.WriteKeyFile(envKeysFilepath ?? ".env.keys", result.KeysSrc);
        }

        foreach (ProcessedSet processedEnv in result.ProcessedEnvs)
        {
            options?.Logger?.Verbose($"setting for {processedEnv.EnvFilePath}");

            if (processedEnv.Error == null)
            {
                Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
                options?.Logger?.Verbose($"{key} set{withEncryption} ({processedEnv.EnvFilePath})");
                options?.Logger?.Debug(
                    $"{key} set{withEncryption} to {processedEnv.Value} ({processedEnv.EnvFilePath})");
            }
            else
            {
                options?.Logger?.Warn((processedEnv.Error as DotenvxException)?.MessageWithHelp ??
                                       processedEnv.Error.Message);
            }

            events?.File(processedEnv.EnvFilePath, processedEnv.Changed, processedEnv.Error,
                new Dictionary<string, object?> { ["key"] = key, ["output"] = "file" });
        }

        events?.Add(new Dictionary<string, object?>
        {
            ["error_count"] = (long)result.ProcessedEnvs.Count(env => env.Error is not null)
        });

        LogSetSummary.Log(options?.Logger, key, encrypt, result.ProcessedEnvs, result.ChangedFilePaths,
            result.UnchangedFilePaths);

        return result;
    }
}
