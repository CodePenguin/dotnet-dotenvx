using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers
{
    public static class BuildEnvs
    {
        public static List<EnvItem> Execute(ConfigOptions options, string dotenvKey)
        {
            var optionPaths = DotenvOptionPaths.Execute(options);
            var envs = new List<EnvItem>();

            if (!string.IsNullOrEmpty(options.Convention))
            {
                envs.AddRange(Conventions.Execute(options.Convention));
            }

            // Ignoring vault logic. We only handle the case where DOTENV_KEY is not set.
            if (string.IsNullOrEmpty(dotenvKey))
            {
                foreach (var optionPath in optionPaths)
                {
                    envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = optionPath });
                }
            }

            return envs;
        }
    }
}
