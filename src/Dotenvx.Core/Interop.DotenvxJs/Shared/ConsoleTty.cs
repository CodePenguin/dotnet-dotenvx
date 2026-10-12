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

    internal static void ResetToDefaults()
    {
        IsStdOutTty = DefaultIsStdOutTty;
        IsStdErrTty = DefaultIsStdErrTty;
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
