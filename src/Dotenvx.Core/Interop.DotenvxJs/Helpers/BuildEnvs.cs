using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers
{
    public static partial class Helpers
    {
        internal static List<EnvItem> BuildEnvs(ConfigOptions options)
        {
            var optionPaths = DotenvOptionPaths(options);
            var envs = new List<EnvItem>();

            if (!string.IsNullOrEmpty(options.Convention))
            {
                envs.AddRange(Conventions(options.Convention));
            }
            
            foreach (var optionPath in optionPaths)
            {
                envs.Add(new EnvItem { Type = Types.EnvType.EnvFile, Value = optionPath });
            }

            return envs;
        }
    }
}
