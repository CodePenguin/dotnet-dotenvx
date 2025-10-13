using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers
{
    public static class BuildEnvs
    {
        public static List<EnvItem> Execute(ConfigOptions options)
        {
            var optionPaths = DotenvOptionPaths.Execute(options);
            var envs = new List<EnvItem>();

            if (!string.IsNullOrEmpty(options.Convention))
            {
                envs.AddRange(Conventions.Execute(options.Convention));
            }
            
            foreach (var optionPath in optionPaths)
            {
                envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = optionPath });
            }

            return envs;
        }
    }
}
