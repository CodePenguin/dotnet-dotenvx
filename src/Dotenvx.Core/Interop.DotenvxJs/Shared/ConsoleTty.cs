using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Shared;

/// <remarks>
/// Delegate-swap seam (matches Fsx.cs's convention) for whether stdout/stderr are attached to a
/// real terminal. Console.IsOutputRedirected/IsErrorRedirected can't be overridden directly, and a
/// test runner always redirects both, so GetColorDepth's tests need a way to simulate "really is a
/// TTY" without real console control.
/// </remarks>
public static class ConsoleTty
{
    public static Func<bool> IsStdOutTty { get; internal set; } = DefaultIsStdOutTty;
    public static Func<bool> IsStdErrTty { get; internal set; } = DefaultIsStdErrTty;

    /// <summary>`process.stdin.isTTY`.</summary>
    public static Func<bool> IsStdInTty { get; internal set; } = DefaultIsStdInTty;

    /// <summary>`process.platform` / `os.release()` as the colour-depth probe sees them.</summary>
    public static OsPlatform Platform { get; internal set; } = OsPlatform.Current;

    internal static void ResetToDefaults()
    {
        Platform = OsPlatform.Current;
        IsStdOutTty = DefaultIsStdOutTty;
        IsStdErrTty = DefaultIsStdErrTty;
        IsStdInTty = DefaultIsStdInTty;
    }

    private static bool DefaultIsStdInTty()
    {
        return !Console.IsInputRedirected;
    }

    private static bool DefaultIsStdOutTty()
    {
        return !Console.IsOutputRedirected;
    }

    private static bool DefaultIsStdErrTty()
    {
        return !Console.IsErrorRedirected;
    }
}
