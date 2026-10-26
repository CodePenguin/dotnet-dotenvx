using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Types;

using Colors = Dotenvx.Interop.DotenvxJs.Shared.Shared;

namespace Dotenvx;

public static class Dotenvx
{
    /// <summary>`logger`: what the library logs through when an options object has no <see cref="ConfigOptions.Logger" />.</summary>
    public static DotenvxConsoleLogger Logger { get; } = new();

    public static ConfigResult Config(ConfigOptions? options = null)
    {
        return Main.Config(options);
    }

    public static object? Get(string? key, GetOptions? options = null)
    {
        return Main.Get(key, options);
    }

    /// <summary>`getSync`: <see cref="Get" /> without anything interactive.</summary>
    public static object? GetSync(string? key, GetOptions? options = null)
    {
        return Main.GetSync(key, options);
    }

    public static IReadOnlyDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        return Main.Parse(src, options);
    }

    public static SetsResult Set(string key, string value, SetsOptions? options = null)
    {
        return Main.Set(key, value, options);
    }

    /// <summary>`setLogLevel(options)`: sets <see cref="Logger" />'s level from `Debug`, `Verbose`, `Quiet` or `LogLevel`; nothing changes when none is given.</summary>
    public static void SetLogLevel(ConfigOptions options)
    {
        Main.SetLogLevel(Logger, options);
    }

    /// <summary>`ls`: the paths of the `.env*` files under <paramref name="directory" />.</summary>
    public static string[] Ls(string directory = "./", string[]? envFile = null, string[]? excludeEnvFile = null)
    {
        return Main.Ls(directory, envFile, excludeEnvFile);
    }

    /// <summary>`withEvents`: an SDK client that reports calls to connected Armor.</summary>
    public static EventSdk WithEvents()
    {
        return Main.WithEvents();
    }

    /// <summary>`getColor(color, stream)`: a function that colors a message for what the stream's terminal supports.</summary>
    public static Func<string, string> GetColor(string color, ColorStream stream)
    {
        return Colors.GetColor(color, stream);
    }

    /// <summary>`bold(message, stream)`.</summary>
    public static string Bold(string message, ColorStream stream)
    {
        return Colors.Bold(message, stream);
    }
}
