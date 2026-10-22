using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/dotenvOptionPaths.js</remarks>
internal static partial class Helpers
{
    private static string[] DotenvOptionPaths(ConfigOptions options)
    {
        if (options.Path != null)
        {
            return [.. options.Path.Select(ResolveHome)];
        }

        return options.PathString?.Split(',').Select(filepath => filepath.Trim()).Where(filepath => filepath.Length > 0)
            .Select(ResolveHome).ToArray() ?? [];
    }
}
