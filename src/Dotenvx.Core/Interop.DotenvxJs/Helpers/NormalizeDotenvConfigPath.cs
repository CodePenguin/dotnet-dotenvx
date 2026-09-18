using System.Collections;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/normalizeDotenvConfigPath.js</remarks>
public static partial class Helpers
{
    public static EnvItem[] NormalizeDotenvConfigPath(EnvItem[] envs, IDictionary<string, string?>? processEnv = null)
    {
        if (envs.Any(env => env.Type == EnvType.EnvFile))
        {
            return envs;
        }

        processEnv ??= Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);

        processEnv.TryGetValue("DOTENV_FILE", out string? configuredEnvFiles);
        if (string.IsNullOrEmpty(configuredEnvFiles))
        {
            processEnv.TryGetValue("DOTENV_PATH", out configuredEnvFiles);
        }

        if (string.IsNullOrEmpty(configuredEnvFiles))
        {
            processEnv.TryGetValue("DOTENV_F", out configuredEnvFiles);
        }

        if (string.IsNullOrEmpty(configuredEnvFiles))
        {
            return envs;
        }

        EnvItem[] envFiles = configuredEnvFiles
            .Split(',')
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Select(value => new EnvItem { Type = EnvType.EnvFile, Value = value })
            .ToArray();

        return [..envFiles, ..envs];
    }
}
