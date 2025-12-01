using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/determineEnvs.js</remarks>
public static partial class Helpers
{
    private static readonly EnvItem[] DefaultEnvs = [new() { Type = EnvType.EnvFile, Value = ".env" }];

    public static EnvItem[] DetermineEnvs(EnvItem[]? envs, IDictionary<string, string> processEnv)
    {
        string[] privateKeyNames = DotenvPrivateKeyNames(processEnv);

        if (envs == null || envs.Length == 0)
        {
            return privateKeyNames.Length != 0
                ? DetermineEnvsFromDotenvPrivateKey(privateKeyNames)
                : [..DefaultEnvs];
        }

        bool fileAlreadySpecified = envs.Any(env => env.Type == EnvType.EnvFile);

        if (fileAlreadySpecified)
        {
            return envs;
        }

        List<EnvItem> combinedEnvs = new(DefaultEnvs);
        combinedEnvs.AddRange(envs);
        return combinedEnvs.ToArray();
    }

    private static EnvItem[] DetermineEnvsFromDotenvPrivateKey(string[] privateKeyNames)
    {
        List<EnvItem> envs = [];
        foreach (string privateKeyName in privateKeyNames)
        {
            string filename = GuessPrivateKeyFilename(privateKeyName);
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = filename });
        }

        return envs.ToArray();
    }
}