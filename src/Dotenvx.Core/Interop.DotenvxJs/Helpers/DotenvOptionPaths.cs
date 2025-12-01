using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/dotenvOptionPaths.js</remarks>
public static partial class Helpers
{
    private static string[] DotenvOptionPaths(ConfigOptions options)
    {
        return options.Path?.Select(ResolveHome).ToArray() ?? [];
    }
}