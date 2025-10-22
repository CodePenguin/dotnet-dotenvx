using System.Collections;

using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public class Get(string? key, List<EnvItem> envs, bool overload, bool all, string? envKeysFilepath)
{
    public GetResult Run()
    {
        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        RunResult runResult = new Run(envs, overload, envKeysFilepath, processEnv).Execute();

        List<Exception> errors = [];
        foreach (ProcessedEnv processedEnv in runResult.ProcessedEnvs)
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

            return new GetResult { Parsed = parsed, Errors = errors };
        }
        else
        {
            if (all)
            {
                return new GetResult { Parsed = processEnv, Errors = errors };
            }

            Dictionary<string, string> parsed = [];
            foreach (ProcessedEnv processedEnv in runResult.ProcessedEnvs)
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

            return new GetResult { Parsed = parsed, Errors = errors };
        }
    }
}