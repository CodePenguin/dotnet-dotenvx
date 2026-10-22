using System.Diagnostics;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/openUrl.js (`open(url, { wait: false })` from @dotenvx/tooling).</remarks>
internal static class OpenUrl
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
        // the url comes from the Armor server: only ever open web pages
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        ProcessStartInfo psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(url) { UseShellExecute = true }
            : OperatingSystem.IsMacOS()
                ? new ProcessStartInfo("open", url) { UseShellExecute = false }
                : new ProcessStartInfo("xdg-open", url) { UseShellExecute = false };

        Process.Start(psi);
    }
}
