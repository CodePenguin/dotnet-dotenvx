using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/packageJson.js (name, version, description of the dotenvx package).</remarks>
internal static class PackageJson
{
    private const string PinnedVersion = "2.38.2";

    public static string Version { get; internal set; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] is { Length: > 0 } version && Assembly.GetEntryAssembly()?.GetName().Name == "dotenvx"
            ? version
            : PinnedVersion;
}

/// <remarks>Reference source: `systeminformation` as used by dotenvx/src/db/session.js (`system_uuid`, `os_platform`, `os_arch`).</remarks>
internal static class SystemInformation
{
    public record Info(string SystemUuid, string OsPlatform, string OsArch);

    private static readonly Func<Info> DefaultRead = Read;

    public static Func<Info> Current { get; internal set; } = DefaultRead;

    internal static void ResetToDefaults()
    {
        Current = DefaultRead;
    }

    private static Info Read()
    {
        string platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "darwin" : "linux";
        string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X86 => "ia32",
            var other => other.ToString().ToLowerInvariant()
        };

        return new Info(SystemUuid(), platform, arch);
    }

    private static string SystemUuid()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // systeminformation runs the system PowerShell by absolute path, never whatever PATH finds first
                string powershell = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                SecretCliResult result = SecretCli.Exec(powershell,
                    ["-NoProfile", "-NonInteractive", "-Command",
                        "(Get-CimInstance Win32_ComputerSystemProduct).UUID"], null);

                return result.Stdout.Trim().ToLowerInvariant();
            }

            if (OperatingSystem.IsMacOS())
            {
                SecretCliResult result = SecretCli.Exec("ioreg", ["-rd1", "-c", "IOPlatformExpertDevice"], null);
                System.Text.RegularExpressions.Match match =
                    System.Text.RegularExpressions.Regex.Match(result.Stdout, "\"IOPlatformUUID\" = \"([^\"]+)\"");

                return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";
            }

            // systeminformation's system().uuid reads only the DMI product_uuid on Linux (/etc/machine-id belongs to its separate uuid().os):
            // machines without DMI data (WSL, many containers) report an empty uuid
            if (File.Exists("/sys/class/dmi/id/product_uuid"))
            {
                return File.ReadAllText("/sys/class/dmi/id/product_uuid").Trim().ToLowerInvariant();
            }
        }
        catch (Exception)
        {
            // systeminformation reports an empty uuid when it cannot read one
        }

        return "";
    }
}
