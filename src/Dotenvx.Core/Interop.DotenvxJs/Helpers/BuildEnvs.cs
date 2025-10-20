using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    internal static List<EnvItem> BuildEnvs(ConfigOptions options)
    {
        List<string> optionPaths = DotenvOptionPaths(options);
        List<EnvItem> envs = new();

        if (!string.IsNullOrEmpty(options.Convention))
        {
            envs.AddRange(Conventions(options.Convention));
        }

        foreach (string optionPath in optionPaths)
        {
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = optionPath });
        }

        return envs;
    }
}