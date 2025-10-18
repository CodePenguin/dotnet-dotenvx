using System.Collections;

using Dotenvx.Core.Interop.DotenvxJs.Helpers;
using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs;

public static class Main
{
    public static ConfigResult Config(ConfigOptions options)
    {
        // allow user to set processEnv to write to
        IDictionary<string, string> processEnv = options.ProcessEnv ?? new Dictionary<string, string>(Environment
            .GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

        // ignore
        List<string> ignore = options.Ignore ?? [];

        // strict
        bool strict = options.Strict;

        // envKeysFile
        string envKeysFile = options.EnvKeysFile ?? string.Empty;

        try
        {
            List<EnvItem> envs = Helpers.Helpers.BuildEnvs(options);

            RunResult runResult = new Run(envs, options.Overload, envKeysFile, options.ProcessEnv).Execute();

            Exception? lastError = null;
            IDictionary<string, string> parsedAll = new Dictionary<string, string>();

            foreach (ProcessedEnv processedEnv in runResult.ProcessedEnvs)
            {
                foreach (Exception error in processedEnv.Errors)
                {
                    string errorCode = error is DotenvxException dotenvxError ? dotenvxError.Code : "GENERIC_ERROR";

                    // Check if the error code is in the ignore list
                    bool isIgnored = ignore.Any(ignoreCode => errorCode.Contains(ignoreCode));
                    if (isIgnored)
                    {
                        continue;
                    }

                    if (strict)
                    {
                        throw error; // Re-throw the original exception if strict mode is enabled
                    }

                    lastError = error;

                    if (errorCode == "MISSING_ENV_FILE")
                    {
                        if (string.IsNullOrEmpty(options.Convention))
                        {
                            // Logger.Error(errorMessage);
                        }
                    }
                    // Logger.Error(errorMessage);
                }

                foreach (KeyValuePair<string, string> entry in processedEnv.Injected)
                {
                    parsedAll[entry.Key] = entry.Value;
                }

                foreach (KeyValuePair<string, string> entry in processedEnv.PreExisted)
                {
                    parsedAll[entry.Key] = entry.Value;
                }
            }

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

            return new ConfigResult { Parsed = new Dictionary<string, string>(), Error = error };
        }
    }

    public static IDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        IDictionary<string, string> processEnv = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? string.Empty);

        if (options?.ProcessEnv != null)
        {
            processEnv = options.ProcessEnv;
        }

        // private decryption key
        string? privateKey = options?.PrivateKey ?? null;

        // overload
        bool overload = options?.Overload ?? false;

        ParseResult response = new Parse(src, privateKey, processEnv ?? new Dictionary<string, string>(), overload)
            .Execute();

        // display any errors
        // TODO: Need to log errors

        return response.Parsed;
    }
}