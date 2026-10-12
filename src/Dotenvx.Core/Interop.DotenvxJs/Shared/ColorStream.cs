namespace Dotenvx.Interop.DotenvxJs.Shared;

/// <remarks>
/// Mirrors dotenvx/src/shared/colors.js's getColor(color, stream)/bold(message, stream) taking a
/// concrete Node stream (process.stdout or process.stderr) - color/depth depends on whether THAT
/// specific stream is a TTY, not a single global setting, since a caller can pipe stdout while
/// stderr stays attached to a real terminal (or vice versa).
/// </remarks>
public enum ColorStream
{
    StdOut,
    StdErr
}
