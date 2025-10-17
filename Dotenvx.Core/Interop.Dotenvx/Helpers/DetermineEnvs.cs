using Dotenvx.Core.Interop.Dotenvx.Services;
using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static partial class Helpers
{
    private static readonly List<EnvItem> DefaultEnvs = [new() { Type = EnvType.EnvFile, Value = ".env" }];

    internal static List<EnvItem> DetermineEnvs(List<EnvItem>? envs, IDictionary<string, string> processEnv)
    {
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
            var filename = Helpers.GuessPrivateKeyFilename(privateKeyName);
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = filename });
        }
        return envs;
    }
}