using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static class DotenvOptionPaths
{
    public static List<string> Execute(ConfigOptions options)
    {
        return options.Path != null 
            ? options.Path.Select(ResolveHome.Execute).ToList()
            : [];
    }
}
