using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx;

public static class Dotenvx
{
    public static ConfigResult Config(ConfigOptions? options = null)
    {
        return Main.Config(options);
    }

    public static object? Get(string? key, GetOptions? options = null)
    {
        return Main.Get(key, options);
    }

    public static IReadOnlyDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        return Main.Parse(src, options);
    }

    public static SetsResult Set(string key, string value, SetsOptions? options = null)
    {
        return Main.Set(key, value, options);
    }
}