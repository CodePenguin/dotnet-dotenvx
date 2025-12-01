using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Shared;

public static class Shared
{
    private static readonly Dictionary<string, int> Colors16 = new()
    {
        { "blue", 34 },
        { "gray", 37 },
        { "green", 32 },
        { "olive", 33 },
        { "orangered", 31 }, // mapped to red
        { "plum", 35 }, // mapped to magenta
        { "red", 31 },
        { "electricblue", 36 },
        { "dodgerblue", 36 }
    };

    private static readonly Dictionary<string, int> Colors256 = new()
    {
        { "blue", 21 },
        { "gray", 244 },
        { "green", 34 },
        { "olive", 142 },
        { "orangered", 202 },
        { "plum", 182 },
        { "red", 196 },
        { "electricblue", 45 },
        { "dodgerblue", 33 }
    };

    public static Func<string, string> GetColor(string color)
    {
        if (!Colors256.TryGetValue(color, out int value))
        {
            throw new ArgumentException($"Invalid color {color}");
        }

        int colorDepth = GetColorDepth();

        switch (colorDepth)
        {
            case >= 8:
                {
                    int code = value;
                    return message => $"\x1b[38;5;{code}m{message}\x1b[39m";
                }
            case >= 4:
                {
                    int code = Colors16[color];
                    return message => $"\x1b[{code}m{message}\x1b[39m";
                }
            default:
                return message => message;
        }
    }

    public static string Bold(string message)
    {
        return GetColorDepth() >= 4 ? $"\x1b[1m{message}\x1b[22m" : message;
    }
}