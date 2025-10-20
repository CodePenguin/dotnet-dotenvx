using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static readonly string[] ValidEnvs = ["development", "test", "production"];

    public static List<EnvItem> Conventions(string convention)
    {
        string env = Environment.GetEnvironmentVariable("DOTENV_ENV") ??
                     Environment.GetEnvironmentVariable("NODE_ENV") ?? "development";

        switch (convention)
        {
            case "nextjs":
                string? canonicalEnv = ValidEnvs.Contains(env) ? env : null;

                return new[]
                {
                    canonicalEnv != null
                        ? new EnvItem { Type = EnvType.EnvFile, Value = $".env.{canonicalEnv}.local" }
                        : null,
                    canonicalEnv != "test"
                        ? new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" }
                        : null,
                    canonicalEnv != null
                        ? new EnvItem { Type = EnvType.EnvFile, Value = $".env.{canonicalEnv}" }
                        : null,
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env" }
                }.Where(x => x != null).OfType<EnvItem>().ToList();
            case "flow":
                return new[]
                {
                    new EnvItem { Type = EnvType.EnvFile, Value = $".env.{env}.local" },
                    new EnvItem { Type = EnvType.EnvFile, Value = $".env.{env}" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env.defaults" }
                }.ToList();
            default:
                throw new Exception($"INVALID_CONVENTION: '{convention}'. permitted conventions: ['nextjs', 'flow']");
        }
    }
}