using System.Collections;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/get.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Get(string? key, EnvItem[] envs, bool overload, bool all, string? envKeysFilePath)
{
    private static readonly Func<string?, EnvItem[], bool, bool, string?, Get> DefaultFactory =
        (key, envs, overload, all, envKeysFile) => new Get(key, envs, overload, all, envKeysFile);

    public static Func<string?, EnvItem[], bool, bool, string?, Get>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual GetResult Run()
    {
        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        RunResult runResult = Services.Run.Factory(envs, overload, processEnv, envKeysFilePath).Execute();

        List<Exception> errors = [];
        foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
        {
            errors.AddRange(processedEnv.Errors);
        }

        if (key != null)
        {
            Dictionary<string, string> parsed = [];
            if (processEnv.TryGetValue(key, out string? value))
            {
                parsed[key] = value;
            }
            else
            {
                errors.Add(new MissingKeyException(key));
            }

            return new GetResult { Parsed = parsed, Errors = errors.ToArray() };
        }
        else
        {
            if (all)
            {
                return new GetResult { Parsed = processEnv, Errors = errors.ToArray() };
            }

            Dictionary<string, string> parsed = [];
            foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
            {
                if (processedEnv.Parsed.Count <= 0)
                {
                    continue;
                }

                foreach (string parsedKey in processedEnv.Parsed.Keys)
                {
                    if (processEnv.TryGetValue(parsedKey, out string? value))
                    {
                        parsed[parsedKey] = value;
                    }
                }
            }

            return new GetResult { Parsed = parsed, Errors = errors.ToArray() };
        }
    }
}