using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static readonly List<EnvItem> DefaultEnvs = [new() { Type = Types.EnvType.EnvFile, Value = ".env" }];

    internal static List<EnvItem> DetermineEnvs(List<EnvItem>? envs, IDictionary<string, string> processEnv)
    {
        List<string> privateKeyNames = DotenvPrivateKeyNames.Execute(processEnv);

        if (envs == null || envs.Count == 0)
        {
            return privateKeyNames.Count != 0
                ? DetermineEnvsFromDotenvPrivateKey(privateKeyNames)
                : [..DefaultEnvs];
        }

        bool fileAlreadySpecified = envs.Any(env => env.Type == Types.EnvType.EnvFile);

        if (fileAlreadySpecified)
        {
            return envs;
        }

        List<EnvItem> combinedEnvs = new(DefaultEnvs);
        combinedEnvs.AddRange(envs);
        return combinedEnvs;
    }

    private static List<EnvItem> DetermineEnvsFromDotenvPrivateKey(List<string> privateKeyNames)
    {
        List<EnvItem> envs = new();
        foreach (string privateKeyName in privateKeyNames)
        {
            string filename = GuessPrivateKeyFilename(privateKeyName);
            envs.Add(new EnvItem { Type = Types.EnvType.EnvFile, Value = filename });
        }

        return envs;
    }
}