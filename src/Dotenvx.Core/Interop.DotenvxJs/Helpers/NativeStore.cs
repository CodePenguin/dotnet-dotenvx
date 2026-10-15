using System.Text;
using System.Text.Json;

using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>An OS secret store holding dotenvx private keys, addressed by public key (providers/native/index.js).</summary>
public interface INativeSecretStore
{
    /// <summary>The stored private key, or null when absent. Throws when the store itself fails.</summary>
    string? Get(string publicKey);

    void Set(string publicKey, string privateKey, string label);

    void Delete(string publicKey);
}

/// <remarks>
/// Reference source: dotenvx/src/lib/providers/native/index.js, which dispatches on `process.platform` to
/// helpers/macosKeychain.js, linuxSecretService.js and windowsCredentialManager.js. Every store shells out exactly like
/// upstream (`/usr/bin/security`, `secret-tool`, and `powershell.exe` with upstream's script) through
/// <see cref="SecretCli" />, so the entries are byte-for-byte the ones Node writes and reads.
/// </remarks>
public static class NativeStore
{
    private const string Service = "dotenvx";

    private static INativeSecretStore? _override;

    /// <summary>`process.platform`: "darwin", "linux" or "win32" on the supported systems. Swappable for tests.</summary>
    public static string Platform { get; internal set; } = DetectPlatform();

    /// <summary>
    /// The store for the current platform (providers/native/index.js dispatches on `process.platform` per call).
    /// Assigning replaces it entirely so unit tests never touch a real OS store.
    /// </summary>
    public static INativeSecretStore Current
    {
        get => _override ?? ForPlatform(Platform);
        internal set => _override = value;
    }

    /// <summary>`['darwin', 'linux', 'win32'].includes(process.platform)`.</summary>
    public static bool IsSupportedPlatform => Platform is "darwin" or "linux" or "win32";

    /// <summary>providers/native/index.js `get(key)`.</summary>
    public static string? Get(string key)
    {
        return Current.Get(key);
    }

    /// <summary>providers/native/index.js `set(key, value, label = key)`.</summary>
    public static void Set(string key, string value, string? label = null)
    {
        Current.Set(key, value, label ?? key);
    }

    /// <summary>providers/native/index.js `delete(key)`.</summary>
    public static void Delete(string key)
    {
        Current.Delete(key);
    }

    /// <summary>The store name upstream prints in its NOT_FOUND message.</summary>
    public static string StoreName =>
        Platform == "win32" ? "Windows Credential Manager"
        : Platform == "linux" ? "Linux Secret Service"
        : "macOS Keychain";

    internal static void ResetToDefaults()
    {
        _override = null;
        Platform = DetectPlatform();
    }

    private static string DetectPlatform()
    {
        return OperatingSystem.IsWindows() ? "win32"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsMacOS() ? "darwin"
            : "other";
    }

    /// <summary>win32 and linux have their own store; everything else falls through to macOS, like upstream's last branch.</summary>
    internal static INativeSecretStore ForPlatform(string platform)
    {
        return platform switch
        {
            "win32" => new WindowsCredentialManager(),
            "linux" => new LinuxSecretService(),
            _ => new MacosKeychain()
        };
    }

    // nativeStoreError.js: ENOENT, and on Linux a missing D-Bus session, mean "no store here"; anything else is a failed access
    private static NativeStoreException StoreError(string message, SecretCliResult? result, bool linux = false)
    {
        bool unavailable = result is not null && (result.NotFound || (linux && LinuxUnavailable.IsMatch(result.Stderr)));

        return new NativeStoreException(unavailable ? "NATIVE_UNAVAILABLE" : "NATIVE_ACCESS_FAILED", message);
    }

    private static readonly System.Text.RegularExpressions.Regex LinuxUnavailable = new(
        @"org\.freedesktop\.DBus\.Error\.(ServiceUnknown|NoServer)|Cannot autolaunch D-Bus without X11|Failed to connect to socket .*: No such file or directory",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <remarks>Reference source: dotenvx/src/lib/helpers/macosKeychain.js</remarks>
    internal sealed class MacosKeychain : INativeSecretStore
    {
        private const string SecurityBin = "/usr/bin/security";

        private static string QuoteArgument(string value)
        {
            if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                throw new InvalidOperationException("invalid Keychain argument");
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public string? Get(string publicKey)
        {
            string[] args = ["find-generic-password", "-s", Service, "-a", publicKey, "-w"];
            SecretCliResult result = SecretCli.Exec(SecurityBin, args, null);
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to read private key from macOS Keychain", result);
            }

            return result.Stdout.Trim();
        }

        public void Set(string publicKey, string privateKey, string label)
        {
            SecretCliResult? result = null;
            try
            {
                string input = string.Join(' ',
                    new[] { "add-generic-password", "-U", "-s", Service, "-a", publicKey, "-l", label, "-w", privateKey }
                        .Select(QuoteArgument)) + "\n";
                // the 4096-byte line buffer of security must also consume the newline and NUL.
                if (System.Text.Encoding.UTF8.GetByteCount(input) >= 4096)
                {
                    throw new InvalidOperationException("Keychain command too long");
                }

                // Interactive security can exit zero on failure, so inspect stderr too.
                result = SecretCli.ExecWithInput(SecurityBin, ["-i"], input);
                if (result.NotFound || result.ExitCode != 0 || result.Stderr.Length > 0)
                {
                    throw new InvalidOperationException("Keychain write failed");
                }
            }
            catch (Exception)
            {
                throw StoreError("failed to save private key to macOS Keychain", result);
            }
        }

        public void Delete(string publicKey)
        {
            SecretCliResult result = SecretCli.Exec(SecurityBin,
                ["delete-generic-password", "-s", Service, "-a", publicKey], null);
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to delete private key from macOS Keychain", result);
            }
        }
    }

    /// <remarks>Reference source: dotenvx/src/lib/helpers/linuxSecretService.js</remarks>
    internal sealed class LinuxSecretService : INativeSecretStore
    {
        private const string SecretToolBin = "secret-tool";

        private static string[] Attributes(string publicKey)
        {
            return ["service", Service, "public-key", publicKey];
        }

        public string? Get(string publicKey)
        {
            SecretCliResult result = SecretCli.Exec(SecretToolBin, ["lookup", .. Attributes(publicKey)], null);
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to read private key from Linux Secret Service", result, true);
            }

            string value = result.Stdout.Trim();

            return value == "" ? null : value;
        }

        public void Set(string publicKey, string privateKey, string label)
        {
            SecretCliResult result = SecretCli.ExecWithInput(SecretToolBin,
                ["store", $"--label={label}", .. Attributes(publicKey)], privateKey);
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to save private key to Linux Secret Service", result, true);
            }
        }

        public void Delete(string publicKey)
        {
            SecretCliResult result = SecretCli.Exec(SecretToolBin, ["clear", .. Attributes(publicKey)], null);
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to delete private key from Linux Secret Service", result, true);
            }
        }
    }

    /// <remarks>Reference source: dotenvx/src/lib/helpers/windowsCredentialManager.js (same embedded PowerShell script)</remarks>
    internal sealed class WindowsCredentialManager : INativeSecretStore
    {
        private const string PowershellBin = "powershell.exe";

        private static readonly string EncodedScript =
            Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsCredentialScript.Script));

        private static string Target(string publicKey)
        {
            return $"{Service}:{publicKey}";
        }

        /// <summary>`run(payload)`: the script reads the JSON payload from stdin; any failure throws.</summary>
        private static SecretCliResult Run(object payload)
        {
            return SecretCli.ExecWithInput(PowershellBin,
                ["-NoProfile", "-NonInteractive", "-EncodedCommand", EncodedScript], JsonSerializer.Serialize(payload));
        }

        public string? Get(string publicKey)
        {
            SecretCliResult result = Run(new { action = "read", target = Target(publicKey) });
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to read private key from Windows Credential Manager", result);
            }

            string value = result.Stdout.Trim();

            return value == "" ? null : value;
        }

        public void Set(string publicKey, string privateKey, string label)
        {
            SecretCliResult result = Run(new { action = "write", target = Target(publicKey), username = publicKey, secret = privateKey });
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to save private key to Windows Credential Manager", result);
            }
        }

        public void Delete(string publicKey)
        {
            SecretCliResult result = Run(new { action = "delete", target = Target(publicKey) });
            if (result.NotFound || result.ExitCode != 0)
            {
                throw StoreError("failed to delete private key from Windows Credential Manager", result);
            }
        }
    }
}
