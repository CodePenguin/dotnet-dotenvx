using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/buildEnvs.js</remarks>
public static partial class Helpers
{
    internal static EnvItem[] BuildEnvs(ConfigOptions options)
    {
        string[] optionPaths = DotenvOptionPaths(options);
        List<EnvItem> envs = [];

        if (!string.IsNullOrEmpty(options.Convention))
        {
            envs.AddRange(Conventions(options.Convention));
        }

        foreach (string optionPath in optionPaths)
        {
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = optionPath });
        }

        return envs.ToArray();
    }
}