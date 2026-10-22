using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/normalizeDotenvConfigQuiet.js</remarks>
internal static partial class Helpers
{
    public static ConfigOptions NormalizeDotenvConfigQuiet(ConfigOptions options)
    {
        string? quiet = Environment.GetEnvironmentVariable("DOTENV_QUIET") ??
                        Environment.GetEnvironmentVariable("DOTENV_CONFIG_QUIET");

        if (quiet == "true")
        {
            options.Quiet = true;
        }

        return options;
    }
}
