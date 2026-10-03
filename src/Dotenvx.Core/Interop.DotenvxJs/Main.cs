using System.Collections;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/src/lib/main.js</remarks>
public static class Main
{
    public static ConfigResult Config(ConfigOptions? options = null)
    {
        options ??= new ConfigOptions();
        options = NormalizeDotenvConfigQuiet(options);
        options = NormalizeDotenvConfigConvention(options);
        options = NormalizeDotenvConfigIgnore(options);

        IDotenvxLogger? logger = options.Logger;
        SetLogLevel(logger, options);

        // allow user to set processEnv to write to
        bool useSystemEnv = options.ProcessEnv == null;
        IDictionary<string, string> processEnv =
            options.ProcessEnv ??
            new Dictionary<string, string>(Environment
                .GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

        // ignore
        List<string> ignore = options.Ignore ?? [];

        // strict
        bool strict = options.Strict ?? false;

        // envKeysFile
        string envKeysFile = options.EnvKeysFile ?? string.Empty;

        try
        {
            EnvItem[] envs = NormalizeDotenvConfigPath(BuildConfigEnvs(options));

            Run run = Run.Factory(envs, options.Overload ?? false, processEnv, envKeysFile);
            run.DetermineOnExecute = options.Envs == null;
            RunResult runResult = run.Execute();

            if (options.Mask != null)
            {
                object showChar = Equals(options.Mask, true) ? 6 : options.Mask;
                MaskProcessedEnvs(runResult.ProcessedEnvs, processEnv, showChar);
            }

            Exception? lastError = null;
            Dictionary<string, string> parsedAll = new();

            foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
            {
                if (processedEnv.Type == EnvType.EnvFile)
                {
                    logger?.Verbose(
                        $"loading env from {processedEnv.FilePath} ({Path.GetFullPath(processedEnv.FilePath)})");
                }

                foreach (Exception error in processedEnv.Errors)
                {
                    string errorCode = error is DotenvxException dotenvxError ? dotenvxError.Code : "GENERIC_ERROR";

                    // Check if the error code is in the ignore list
                    bool isIgnored = ignore.Contains(errorCode); // exact code match, like `ignore.includes(error.code)`
                    if (isIgnored)
                    {
                        logger?.Verbose($"ignored: {error.Message}");
                        continue;
                    }

                    if (strict)
                    {
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
                logger?.Debug($"parsed: {JsonStringify(processedEnv.Parsed)}");

                foreach (KeyValuePair<string, string> entry in processedEnv.Injected)
                {
                    parsedAll[entry.Key] = entry.Value;

                    // verbose/debug injected key/value
                    logger?.Verbose($"{entry.Key} set");
                    logger?.Debug($"{entry.Key} set to {entry.Value}");

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
                    logger?.Debug($"{entry.Key} pre-exists as {entry.Value} (protip: use --overload to override)");
                }
            }

            string msg = $"injected env ({runResult.UniqueInjectedKeys.Length})";
            if (runResult.ReadableFilePaths.Length > 0)
            {
                msg += $" from {string.Join(", ", runResult.ReadableFilePaths)}";
            }

            logger?.Success($"⟐ {msg}");

            return lastError != null
                ? new ConfigResult { Parsed = parsedAll, Error = lastError }
                : new ConfigResult { Parsed = parsedAll }; // No error
        }
        catch (Exception error)
        {
            if (strict)
            {
                throw; // re-throw if strict
            }

            logger?.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);

            return new ConfigResult { Parsed = new Dictionary<string, string>(), Error = error };
        }
    }

    private static void SetLogLevel(IDotenvxLogger? logger, ConfigOptions options)
    {
        if (logger == null)
        {
            return;
        }

        DotenvxLogLevel logLevel = DotenvxLogLevel.Info; // default
        if (options.Debug == true)
        {
            logLevel = DotenvxLogLevel.Debug;
        }
        else if (options.Verbose == true)
        {
            logLevel = DotenvxLogLevel.Verbose;
        }
        else if (options.Quiet == true)
        {
            logLevel = DotenvxLogLevel.Error;
        }
        else if (!string.IsNullOrEmpty(options.LogLevel))
        {
            logLevel = DotenvxLogLevelExtensions.FromString(options.LogLevel);
        }

        logger.SetLevel(logLevel);
        // Only log which level it's setting if it's not set to quiet mode
        if (options.Quiet != true || (options.Quiet == true && logLevel != DotenvxLogLevel.Error))
        {
            logger.Debug($"Setting log level to {logLevel.ToString().ToLower()}");
        }
    }

    public static object? Get(string? key, GetOptions? options = null)
    {
        options ??= new GetOptions();
        options = (GetOptions)NormalizeDotenvConfigConvention(options);
        options = (GetOptions)NormalizeDotenvConfigIgnore(options);

        EnvItem[] envs = BuildEnvs(options);
        GetResult result = Services.Get
            .Factory(key, envs, options.Overload ?? false, options.All ?? false, options.EnvKeysFile)
            .Run();

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

            if (options.Strict == true)
            {
                throw error;
            }

            options.Logger?.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
        }

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
        Dictionary<string, string?> keypairs = Services.KeyPair.Factory(envFile, envKeysFile).Run();
        return !string.IsNullOrEmpty(key)
            ? new Dictionary<string, string> { { key, keypairs.GetValueOrDefault(key) ?? "" } }
            : keypairs.ToDictionary(kv => kv.Key, kv => kv.Value ?? "");
    }

    public static IReadOnlyDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        options ??= new ParseOptions();
        IDotenvxLogger? logger = options.Logger;
        Dictionary<string, string> processEnv = options.ProcessEnv?.ToDictionary() ??
                                                Environment.GetEnvironmentVariables()
                                                    .Cast<DictionaryEntry>()
                                                    .ToDictionary(entry => (string)entry.Key,
                                                        entry => (string?)entry.Value ?? string.Empty);

        // private decryption key
        string? privateKey = string.IsNullOrEmpty(options.PrivateKey) ? null : options.PrivateKey;

        // overload
        bool overload = options.Overload ?? false;

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

    public static SetsResult Set(string key, string value, SetsOptions? options = null)
    {
        // encrypt
        bool encrypt = !(options?.Plain == true || options?.Encrypt == false);
        bool noCreate = options?.NoCreate == true;
        string withEncryption = encrypt ? " with encryption" : "";

        EnvItem[] envs = BuildEnvs(options ?? new SetsOptions());
        string? envKeysFilepath = options?.EnvKeysFile;

        SetsResult result = Sets.Factory(key, value, envs, encrypt, envKeysFilepath, noCreate).Run();

        if (!string.IsNullOrEmpty(result.KeysSrc))
        {
            Fsx.WriteFileX(envKeysFilepath ?? ".env.keys", result.KeysSrc);
        }

        foreach (ProcessedSet processedEnv in result.ProcessedEnvs)
        {
            if (processedEnv.Error == null)
            {
                Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
            }

            if (processedEnv.Error == null)
            {
                options?.Logger?.Verbose($"{key} set{withEncryption} ({processedEnv.EnvFilePath})");
                options?.Logger?.Debug(
                    $"{key} set{withEncryption} to {processedEnv.Value} ({processedEnv.EnvFilePath})");
            }
            else
            {
                options?.Logger?.Warn((processedEnv.Error as DotenvxException)?.MessageWithHelp ??
                                       processedEnv.Error.Message);
            }
        }

        // this port has no native/armor key storage (out of scope - see CLAUDE.md's submodule-pin
        // note), so remotePrivateKeyAdded/the " · armored ⛨" suffix can never apply here.
        ProcessedSet? localKeyAddedEnv = result.ProcessedEnvs.FirstOrDefault(p => p.PrivateKeyAdded);

        if (result.ChangedFilePaths.Length > 0)
        {
            string msg = encrypt
                ? $"◈ encrypted {key} ({string.Join(',', result.ChangedFilePaths)})"
                : $"◇ set {key} ({string.Join(',', result.ChangedFilePaths)})";
            options?.Logger?.Success(msg);
        }
        else if (encrypt && localKeyAddedEnv != null)
        {
            string keyAddedEnvFilepath = !string.IsNullOrEmpty(localKeyAddedEnv.EnvFilePath)
                ? localKeyAddedEnv.EnvFilePath
                : result.ChangedFilePaths.FirstOrDefault() ?? ".env";
            options?.Logger?.Success($"◈ encrypted {key} ({keyAddedEnvFilepath})");
        }
        else if (result.UnchangedFilePaths.Length > 0)
        {
            options?.Logger?.Info($"○ no change ({string.Join(',', result.UnchangedFilePaths)})");
        }

        return result;
    }
}