using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static List<string> DotenvOptionPaths(ConfigOptions options)
    {
        return options.Path != null
            ? options.Path.Select(ResolveHome).ToList()
            : [];
    }
}