using System.Diagnostics;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/openUrl.js (`open(url, { wait: false })` from @dotenvx/tooling).</remarks>
public static class OpenUrl
{
    private static readonly Action<string> DefaultOpen = LaunchBrowser;

    /// <summary>Swappable so tests never open a real browser.</summary>
    public static Action<string> OpenAction { get; internal set; } = DefaultOpen;

    internal static void ResetToDefaults()
    {
        OpenAction = DefaultOpen;
    }

    public static void Open(string url)
    {
        OpenAction(url);
    }

    private static void LaunchBrowser(string url)
    {
        ProcessStartInfo psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(url) { UseShellExecute = true }
            : OperatingSystem.IsMacOS()
                ? new ProcessStartInfo("open", url) { UseShellExecute = false }
                : new ProcessStartInfo("xdg-open", url) { UseShellExecute = false };

        Process.Start(psi);
    }
}
