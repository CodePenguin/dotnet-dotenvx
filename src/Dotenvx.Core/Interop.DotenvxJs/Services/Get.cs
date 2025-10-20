using System.Collections;
using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public class Get(string? key, List<EnvItem> envs, bool overload, bool all, string? envKeysFilepath)
{
    public GetResult Run()
    {
        var processEnv = new Dictionary<string, string>(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        var run = new Run(envs, overload, envKeysFilepath, processEnv);
        var runResult = run.Execute();

        var errors = new List<Exception>();
        foreach (var processedEnv in runResult.ProcessedEnvs)
        {
            errors.AddRange(processedEnv.Errors);
        }

        if (key != null)
        {
            var parsed = new Dictionary<string, string>();
            if (processEnv.TryGetValue(key, out var value))
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

            var parsed = new Dictionary<string, string>();
            foreach (var processedEnv in runResult.ProcessedEnvs)
            {
                if (processedEnv.Parsed.Count <= 0)
                {
                    continue;
                }

                foreach (var parsedKey in processedEnv.Parsed.Keys)
                {
                    if (processEnv.TryGetValue(parsedKey, out var value))
                    {
                        parsed[parsedKey] = value;
                    }
                }
            }

            return new GetResult { Parsed = parsed, Errors = errors };
        }
    }
}