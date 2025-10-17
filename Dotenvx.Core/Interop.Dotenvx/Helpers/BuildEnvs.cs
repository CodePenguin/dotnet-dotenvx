using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers
{
    public static partial class Helpers
    {
        internal static List<EnvItem> BuildEnvs(ConfigOptions options)
        {
            var optionPaths = Helpers.DotenvOptionPaths(options);
            var envs = new List<EnvItem>();

            if (!string.IsNullOrEmpty(options.Convention))
            {
                envs.AddRange(Helpers.Conventions(options.Convention));
            }
            
            foreach (var optionPath in optionPaths)
            {
                envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = optionPath });
            }

            return envs;
        }
    }
}
