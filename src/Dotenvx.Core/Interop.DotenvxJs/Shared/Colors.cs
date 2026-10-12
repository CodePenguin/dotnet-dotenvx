using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Shared;

/// <remarks>
/// Reference source: dotenvx/src/shared/colors.js. Fixed 2026-09-25: `orangered`/`red`'s 256-color
/// codes had drifted from current upstream (202/196 here vs the real 130/196... actually 130/124),
/// and `amber` (used by `success` - see DotenvxLogger.cs) didn't exist in either color table at
/// all, so `GetColor("amber", ...)` would have thrown `ArgumentException`. Also added the 24-bit
/// true-color branch (`colorsTrueColor` in the JS source) for amber/orangered/red, which this port
/// never implemented even though GetColorDepth can return 24.
/// </remarks>
public static class Shared
{
    private static readonly Dictionary<string, int> Colors16 = new()
    {
        { "amber", 33 },
        { "blue", 34 },
        { "gray", 37 },
        { "green", 32 },
        { "olive", 33 },
        { "orangered", 33 }, // mapped to yellow/brown
        { "plum", 35 }, // mapped to magenta
        { "red", 31 },
        { "electricblue", 36 },
        { "dodgerblue", 36 }
    };

    private static readonly Dictionary<string, int> Colors256 = new()
    {
        { "amber", 136 },
        { "blue", 21 },
        { "gray", 244 },
        { "green", 34 },
        { "olive", 142 },
        { "orangered", 130 }, // burnished copper
        { "plum", 182 },
        { "red", 124 }, // brighter garnet
        { "electricblue", 45 },
        { "dodgerblue", 33 }
    };

    private static readonly Dictionary<string, (int R, int G, int B)> ColorsTrueColor = new()
    {
        { "amber", (236, 213, 63) },
        { "orangered", (138, 90, 43) }, // #8A5A2B burnished copper
        { "red", (140, 35, 50) } // #8C2332 brighter garnet
    };

    public static Func<string, string> GetColor(string color, ColorStream stream)
    {
        if (!Colors256.TryGetValue(color, out int value))
        {
            throw new ArgumentException($"Invalid color {color}");
        }

        int colorDepth = GetColorDepth(stream);

        if (colorDepth >= 24 && ColorsTrueColor.TryGetValue(color, out (int R, int G, int B) rgb))
        {
            return message => $"\x1b[38;2;{rgb.R};{rgb.G};{rgb.B}m{message}\x1b[39m";
        }

        switch (colorDepth)
        {
            case >= 8:
                return message => $"\x1b[38;5;{value}m{message}\x1b[39m";
            case >= 4:
                {
                    int code = Colors16[color];
                    return message => $"\x1b[{code}m{message}\x1b[39m";
                }
            default:
                return message => message;
        }
    }

    public static string Bold(string message, ColorStream stream)
    {
        return GetColorDepth(stream) >= 4 ? $"\x1b[1m{message}\x1b[22m" : message;
    }
}
