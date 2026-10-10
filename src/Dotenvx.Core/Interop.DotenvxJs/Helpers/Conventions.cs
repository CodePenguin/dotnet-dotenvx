using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/conventions.js</remarks>
public static partial class Helpers
{
    private static readonly string[] ValidEnvs = ["development", "test", "production"];

    public static EnvItem[] Conventions(string convention)
    {
        // `process.env.DOTENV_ENV || process.env.NODE_ENV || 'development'`: an empty value is skipped like an unset one
        string env = new[] { Environment.GetEnvironmentVariable("DOTENV_ENV"), Environment.GetEnvironmentVariable("NODE_ENV") }
            .FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "development";

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
                }.Where(x => x != null).OfType<EnvItem>().ToArray();
            case "flow":
                return
                [
                    new EnvItem { Type = EnvType.EnvFile, Value = $".env.{env}.local" },
                    new EnvItem { Type = EnvType.EnvFile, Value = $".env.{env}" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env" },
                    new EnvItem { Type = EnvType.EnvFile, Value = ".env.defaults" }
                ];
            default:
                throw new InvalidConventionException(convention);
        }
    }
}