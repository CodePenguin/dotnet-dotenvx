using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/colorDepth.js (not getColorDepth.js, which doesn't
/// exist at this pin - the doc comment was wrong from the start, fixed 2026-09-25). Missing the
/// upstream function's very first line entirely: `if (stream && !stream.isTTY) return 1` - this
/// port unconditionally fell through to the TERM/COLORTERM-based fallback regardless of whether
/// output was actually a terminal, so every colored message (error/warn/success/etc.) kept its
/// ANSI escape codes even when piped/redirected to a file or another process. Confirmed live via
/// raw byte comparison (`xxd`) against Node, which correctly emits plain text once output isn't a
/// TTY - found while adding a parity test for `ext scan`'s "gitleaks not found" message, which
/// fails a byte-for-byte comparison specifically because of this. The TERM/COLORTERM fallback
/// itself (for when it genuinely IS a TTY) was already a deliberate simplification of Node's own
/// `WriteStream.prototype.getColorDepth()` platform/terminal-capability probing - kept as-is here,
/// only the missing TTY short-circuit was added.
/// </remarks>
public static partial class Helpers
{
    public static int GetColorDepth(ColorStream stream)
    {
        bool isTty = stream == ColorStream.StdOut ? ConsoleTty.IsStdOutTty() : ConsoleTty.IsStdErrTty();
        if (!isTty)
        {
            return 1;
        }

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
