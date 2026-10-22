using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dotenvx.Providers;

/// <summary>An OS secret store failed: <see cref="Code" /> is `NATIVE_UNAVAILABLE` (no store here) or `NATIVE_ACCESS_FAILED`.</summary>
internal sealed class NativeStoreException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <remarks>
/// Reference source: @dotenvx/providers native.js and the macosKeychain.js, linuxSecretService.js and
/// windowsCredentialManager.js read backends. A lookup that fails for any reason is "nothing found", so the parser reports an
/// unresolved key instead. The store is off in CI (`CI` set), with `DOTENVX_NO_NATIVE=true`, `noNative` or `native: false`.
/// </remarks>
public sealed class NativeProvider(ProviderOptions options) : IKeyProvider
{
    private const int TimeoutMs = 10000;
    private const string Service = "dotenvx";

    private static readonly Regex LinuxUnavailable = new(
        @"org\.freedesktop\.DBus\.Error\.(ServiceUnknown|NoServer)|Cannot autolaunch D-Bus without X11|Failed to connect to socket .*: No such file or directory",
        RegexOptions.CultureInvariant);

    /// <summary>`process.platform` for the backend choice: swappable for tests.</summary>
    internal static string Platform { get; set; } = OperatingSystem.IsWindows() ? "win32"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "darwin"
        : "other";

    public Task<IDictionary<string, string>> Get(string publicKey)
    {
        return Task.FromResult(GetSync(publicKey));
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        string? ci = options.Env("CI");
        if (Platform is not ("darwin" or "linux" or "win32") || !string.IsNullOrEmpty(ci) ||
            options.Env("DOTENVX_NO_NATIVE") == "true" || options.NoNative || options.NativeOff)
        {
            return ProviderErrors.Empty();
        }

        try
        {
            string? value = options.NativeGet is { } get ? get(publicKey) : Backend(publicKey);

            return string.IsNullOrEmpty(value)
                ? ProviderErrors.Empty()
                : new Dictionary<string, string> { [publicKey] = value };
        }
        catch (Exception)
        {
            // unresolved keys are reported by the parser
            return ProviderErrors.Empty();
        }
    }

    internal static string? Backend(string publicKey)
    {
        return Platform switch
        {
            "darwin" => Macos(publicKey),
            "linux" => Linux(publicKey),
            _ => Windows(publicKey)
        };
    }

    private static NativeStoreException StoreError(string message, ProcessOutcome outcome, bool linux)
    {
        bool unavailable = outcome.NotFound || (linux && LinuxUnavailable.IsMatch(outcome.Stderr));

        return new NativeStoreException(unavailable ? "NATIVE_UNAVAILABLE" : "NATIVE_ACCESS_FAILED", message);
    }

    private static string Macos(string key)
    {
        ProcessOutcome outcome = ProcessRunner.Run(new ProcessRequest("/usr/bin/security",
            ["find-generic-password", "-s", Service, "-a", key, "-w"], null, TimeoutMs, null, false));
        if (!outcome.Succeeded)
        {
            throw StoreError("failed to read private key from macOS Keychain", outcome, false);
        }

        return outcome.Stdout.Trim();
    }

    private static string? Linux(string publicKey)
    {
        ProcessOutcome outcome = ProcessRunner.Run(new ProcessRequest("secret-tool",
            ["lookup", "service", Service, "public-key", publicKey], null, TimeoutMs, null, true));
        if (!outcome.Succeeded)
        {
            throw StoreError("failed to read private key from Linux Secret Service", outcome, true);
        }

        string value = outcome.Stdout.Trim();

        return value == "" ? null : value;
    }

    private static string? Windows(string publicKey)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsCredentialScript.Script));
        string payload = JsonSerializer.Serialize(new { action = "read", target = $"{Service}:{publicKey}" });
        ProcessOutcome outcome = ProcessRunner.Run(new ProcessRequest("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], payload, TimeoutMs, null, true));
        if (!outcome.Succeeded)
        {
            throw StoreError("failed to read private key from Windows Credential Manager", outcome, false);
        }

        string value = outcome.Stdout.Trim();

        return value == "" ? null : value;
    }
}
