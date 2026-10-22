using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/normalizeDotenvConfigIgnore.js</remarks>
internal static partial class Helpers
{
    public static ConfigOptions NormalizeDotenvConfigIgnore(ConfigOptions options)
    {
        string configuredIgnore = Environment.GetEnvironmentVariable("DOTENV_IGNORE") ??
                                   Environment.GetEnvironmentVariable("DOTENV_CONFIG_IGNORE") ?? "";
        List<string> configIgnore = [.. configuredIgnore
            .Split(',')
            .Select(code => code.Trim())
            .Where(code => code.Length > 0)];

        if (configIgnore.Count < 1)
        {
            return options;
        }

        // JS does `{ ...options, ignore: [...] }` - a full spread that preserves every property
        // (regardless of the caller's actual options shape, e.g. GetOptions' All/Format) without
        // mutating the original. This used to hand-reconstruct a plain `new ConfigOptions {...}`,
        // silently dropping any derived type's own fields whenever DOTENV_IGNORE/
        // DOTENV_CONFIG_IGNORE was actually set - Clone() preserves the runtime type instead.
        ConfigOptions clone = options.Clone();
        clone.Ignore = [.. options.Ignore ?? [], .. configIgnore];
        return clone;
    }
}
