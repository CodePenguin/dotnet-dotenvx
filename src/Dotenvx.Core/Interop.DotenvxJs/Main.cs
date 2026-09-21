using System.Collections;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

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
            EnvItem[] envs = NormalizeDotenvConfigPath(BuildEnvs(options));

            RunResult runResult = Run.Factory(envs, options.Overload ?? false, processEnv, envKeysFile).Execute();

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
                    bool isIgnored = ignore.Any(ignoreCode => errorCode.Contains(ignoreCode));
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

                    if (errorCode == "MISSING_ENV_FILE")
                    {
                        if (!string.IsNullOrEmpty(options.Convention))
                        {
                            continue;
                        }

                        logger?.Error(error.Message);
                        if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
                        {
                            logger?.Error(ex.Help);
                        }
                    }
                    else
                    {
                        logger?.Error(error.Message);
                        if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
                        {
                            logger?.Error(ex.Help);
                        }
                    }
                }

                // debug parsed
                logger?.Debug($"parsed: {JsonSerializer.Serialize(processedEnv.Parsed)}");

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

            string msg = $"injecting env ({runResult.UniqueInjectedKeys.Length})";
            if (runResult.ReadableFilePaths.Length > 0)
            {
                msg += $" from {string.Join(", ", runResult.ReadableFilePaths)}";
            }

            logger?.SuccessV(msg);

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

            logger?.Error(error.Message);
            if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
            {
                logger?.Error(ex.Help);
            }

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
        EnvItem[] envs = BuildEnvs(options);
        GetResult result = Services.Get
            .Factory(key, envs, options.Overload ?? false, options.All ?? false, options.EnvKeysFile)
            .Run();

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

            options.Logger?.Error(error.Message);
            if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
            {
                options.Logger?.Error(ex.Help);
            }
        }

        if (key != null)
        {
            return result.Parsed.GetValueOrDefault(key);
        }

        // If Format is specified, return a formatted string
        if (!string.IsNullOrEmpty(options.Format))
        {
            return options.Format switch
            {
                "eval" or "eval-export" => string.Join('\n', result.Parsed.Select(kv =>
                    $"{(options.Format == "eval-export" ? "export " : "")}{kv.Key}={Escape(kv.Value)}")),
                "shell" => string.Join(' ', result.Parsed.Select(kv => $"{kv.Key}={kv.Value}")),
                "colon" => string.Join(' ', result.Parsed.Select(kv => $"{kv.Key}:{kv.Value}")),
                _ => result.Parsed
            };
        }

        // Otherwise return the dictionary
        return result.Parsed;
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
        IDictionary<string, string> processEnv =
            options.ProcessEnv?.ToDictionary() ??
            Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? string.Empty);

        // private decryption key
        string? privateKey = options.PrivateKey ?? null;

        // overload
        bool overload = options.Overload ?? false;

        ParseResult response = Helpers.Parse.Factory(src, privateKey, processEnv, overload, null).Execute();

        // display any errors
        foreach (Exception error in response.Errors)
        {
            logger?.Error(error.Message);
            if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
            {
                logger?.Error(ex.Help);
            }
        }

        return response.Parsed;
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

        foreach (ProcessedSet processedEnv in result.ProcessedEnvs)
        {
            if (processedEnv.Error == null)
            {
                Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
            }

            if (processedEnv.Error == null)
            {
                options?.Logger?.Verbose($"setting for {processedEnv.EnvFilePath}");
                if (!processedEnv.Changed)
                {
                    continue;
                }

                options?.Logger?.Verbose($"{key} set{withEncryption} ({processedEnv.EnvFilePath})");
                options?.Logger?.Debug(
                    $"{key} set{withEncryption} to {processedEnv.Value} ({processedEnv.EnvFilePath})");
            }
            else
            {
                if (processedEnv.Error.Message.Contains("MISSING_ENV_FILE"))
                {
                    options?.Logger?.Warn(processedEnv.Error.Message);
                    options?.Logger?.Help("? add one with [echo \"HELLO=World\" > .env] and re-run [dotenvx set]");
                }
                else
                {
                    options?.Logger?.Warn(processedEnv.Error.Message);
                    if (processedEnv.Error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
                    {
                        options?.Logger?.Help(ex.Help);
                    }
                }
            }
        }

        if (result.ChangedFilePaths.Length > 0)
        {
            options?.Logger?.Success($"✔ set {key}{withEncryption} ({string.Join(',', result.ChangedFilePaths)})");
        }
        else if (result.UnchangedFilePaths.Length > 0)
        {
            options?.Logger?.Info($"no changes ({string.Join(',', result.UnchangedFilePaths)})");
        }

        foreach (ProcessedSet processedEnv in result.ProcessedEnvs)
        {
            if (!processedEnv.PrivateKeyAdded)
            {
                continue;
            }

            options?.Logger?.Success($"✔ key added to {processedEnv.EnvKeysFilePath} ({processedEnv.PrivateKeyName})");
            options?.Logger?.Help("⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
            options?.Logger?.Help(
                $"⮕  next run [{processedEnv.PrivateKeyName}='{processedEnv.PrivateKey}' dotenvx get {key}] to test decryption locally");
        }

        return result;
    }
}