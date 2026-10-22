using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/normalizeDotenvConfigConvention.js</remarks>
internal static partial class Helpers
{
    public static ConfigOptions NormalizeDotenvConfigConvention(ConfigOptions options)
    {
        string? convention = Environment.GetEnvironmentVariable("DOTENV_CONVENTION");
        if (string.IsNullOrEmpty(convention))
        {
            convention = Environment.GetEnvironmentVariable("DOTENV_CONFIG_CONVENTION");
        }

        if (string.IsNullOrEmpty(options.Convention) && !string.IsNullOrEmpty(convention))
        {
            options.Convention = convention;
        }

        return options;
    }
}
