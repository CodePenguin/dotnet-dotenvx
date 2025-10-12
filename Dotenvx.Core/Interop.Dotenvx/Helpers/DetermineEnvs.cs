using Dotenvx.Core.Interop.Dotenvx.Services;
using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static class DetermineEnvs
{
    private static readonly List<EnvItem> DefaultEnvs = [new EnvItem { Type = EnvType.EnvFile, Value = ".env" }];

    public static List<EnvItem> Execute(List<EnvItem>? envs, IDictionary<string, string> processEnv, string dotenvKey)
    {
        // Ignoring vault logic, this implementation simplifies the original JS logic
        // by not defaulting to .env.vault when DOTENV_KEY is present.

        var privateKeyNames = DotenvPrivateKeyNames.Execute(processEnv);

        if (envs == null || envs.Count == 0)
        {
            return privateKeyNames.Count != 0
                ? DetermineEnvsFromDotenvPrivateKey(privateKeyNames)
                : [..DefaultEnvs];
        }

        bool fileAlreadySpecified = envs.Any(env => env.Type == EnvType.EnvFile);

        if (fileAlreadySpecified)
        {
            return envs;
        }

        var combinedEnvs = new List<EnvItem>(DefaultEnvs);
        combinedEnvs.AddRange(envs);
        return combinedEnvs;
    }

    private static List<EnvItem> DetermineEnvsFromDotenvPrivateKey(List<string> privateKeyNames)
    {
        var envs = new List<EnvItem>();
        foreach (var privateKeyName in privateKeyNames)
        {
            var filename = GuessPrivateKeyFilename.Execute(privateKeyName);
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = filename });
        }
        return envs;
    }
}