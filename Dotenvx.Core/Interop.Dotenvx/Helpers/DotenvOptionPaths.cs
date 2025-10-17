using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static partial class Helpers
{
    internal static List<string> DotenvOptionPaths(ConfigOptions options)
    {
        return options.Path != null 
            ? options.Path.Select(Helpers.ResolveHome).ToList()
            : [];
    }
}
