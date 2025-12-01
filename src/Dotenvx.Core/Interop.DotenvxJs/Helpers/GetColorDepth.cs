using System.Runtime.InteropServices;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/getColorDepth.js</remarks>
public static partial class Helpers
{
    public static int GetColorDepth()
    {
        // Check if we're in a terminal that supports colors
        try
        {
            if (Console.IsOutputRedirected || Console.IsErrorRedirected)
            {
                // Output is redirected, no colors
                return 1;
            }

            // Windows 10+ supports ANSI/VT sequences
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Version version = Environment.OSVersion.Version;
                if (version.Major < 10)
                {
                    return 4; // 16 colors for older Windows
                }

                // Windows 10 build 10586+ supports 256 colors
                return version.Build >= 10586
                    ? 8 // 256 colors
                    : 4; // 16 colors
            }

            // Unix-like systems (Linux, macOS)
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
        catch
        {
            // Fallback to basic color support
            return 4;
        }
    }
}