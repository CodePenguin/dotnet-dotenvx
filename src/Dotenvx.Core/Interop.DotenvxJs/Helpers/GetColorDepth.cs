namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/getColorDepth.js</remarks>
public static partial class Helpers
{
    public static int GetColorDepth()
    {
        // Unix-like systems (Linux, macOS) - simplified to match JS fallback logic
        string? term = Environment.GetEnvironmentVariable("TERM");
        if (string.IsNullOrEmpty(term))
        {
            return 4; // 16 colors as fallback
        }

        if (term.Contains("256color") || term.Contains("xterm"))
        {
            return 8; // 256 colors
        }

        if (term.Contains("truecolor") || term.Contains("24bit"))
        {
            return 24; // 16 million colors
        }

        // Check COLORTERM for truecolor support
        string? colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
        return colorTerm is "truecolor" or "24bit"
            ? 24
            : 4; // 16 colors as fallback
    }
}