using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/packageJson.js (name, version, description of the dotenvx package).</remarks>
public static class PackageJson
{
    private const string PinnedVersion = "2.24.1";

    public static string Version { get; internal set; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] is { Length: > 0 } version && Assembly.GetEntryAssembly()?.GetName().Name == "dotenvx"
            ? version
            : PinnedVersion;
}

/// <summary>A response from <see cref="ArmorHttp.Request" />: status code and the raw body text.</summary>
public record HttpResult(int StatusCode, string Body);

/// <summary>A failed request; <see cref="Code" /> is Node's `error.code` (`ECONNREFUSED`, `ENOTFOUND`, `ERR_INVALID_URL`, ...).</summary>
public class HttpRequestFailedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/http.js (`request` from @dotenvx/tooling, an undici wrapper) and
/// dotenvx/src/lib/helpers/isNetworkError.js. Network failures carry undici's own messages
/// (`connect ECONNREFUSED 127.0.0.1:1`, `getaddrinfo ENOTFOUND host`) since the CLIs print them.
/// </remarks>
public static class ArmorHttp
{
    private static readonly HashSet<string> NetworkErrorCodes =
    [
        "ECONNRESET", "ECONNREFUSED", "ENOTFOUND", "ENETDOWN", "ENETUNREACH", "EHOSTDOWN", "EHOSTUNREACH", "EPIPE",
        "ETIMEDOUT", "UND_ERR_CONNECT_TIMEOUT", "UND_ERR_HEADERS_TIMEOUT", "UND_ERR_BODY_TIMEOUT", "UND_ERR_SOCKET"
    ];

    private static readonly Func<string, string, IReadOnlyDictionary<string, string>?, string?, HttpResult>
        DefaultRequest = Send;

    /// <summary>(url, method, headers, body) => response. Swappable so unit tests never touch the network.</summary>
    public static Func<string, string, IReadOnlyDictionary<string, string>?, string?, HttpResult> Request
    {
        get;
        internal set;
    } = DefaultRequest;

    internal static void ResetToDefaults()
    {
        Request = DefaultRequest;
    }

    public static bool IsNetworkError(Exception? error)
    {
        return error is HttpRequestFailedException failed && NetworkErrorCodes.Contains(failed.Code);
    }

    private static HttpResult Send(string url, string method, IReadOnlyDictionary<string, string>? headers, string? body)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new HttpRequestFailedException("ERR_INVALID_URL", "Invalid URL");
        }

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);

        if (body is not null)
        {
            request.Content = new StringContent(body, new UTF8Encoding(false));
            request.Content.Headers.ContentType = null;
        }

        // undici sends a Content-Type header even on a body-less GET (getAccount.js does); HttpClient only can through content
        if (request.Content is null && headers is not null &&
            headers.Keys.Any(name => name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            request.Content = new ByteArrayContent([]);
        }

        foreach ((string name, string value) in headers ?? new Dictionary<string, string>())
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content is not null)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        try
        {
            using HttpResponseMessage response = client.Send(request);

            return new HttpResult((int)response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
        catch (HttpRequestException error) when (error.InnerException is SocketException socket)
        {
            throw socket.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => new HttpRequestFailedException("ECONNREFUSED",
                    $"connect ECONNREFUSED {Endpoint(uri)}"),
                SocketError.HostNotFound or SocketError.TryAgain => new HttpRequestFailedException("ENOTFOUND",
                    $"getaddrinfo ENOTFOUND {uri.Host}"),
                SocketError.NetworkUnreachable => new HttpRequestFailedException("ENETUNREACH",
                    $"connect ENETUNREACH {Endpoint(uri)}"),
                SocketError.TimedOut => new HttpRequestFailedException("ETIMEDOUT", $"connect ETIMEDOUT {Endpoint(uri)}"),
                SocketError.ConnectionReset => new HttpRequestFailedException("ECONNRESET", "read ECONNRESET"),
                _ => new HttpRequestFailedException("UND_ERR_SOCKET", "other side closed")
            };
        }
        catch (TaskCanceledException)
        {
            throw new HttpRequestFailedException("UND_ERR_BODY_TIMEOUT", "Body Timeout Error");
        }
    }

    private static string Endpoint(Uri uri)
    {
        string host = uri.HostNameType == UriHostNameType.Dns && uri.Host == "localhost" ? "127.0.0.1" : uri.Host;

        return $"{host}:{uri.Port}";
    }
}

/// <remarks>Reference source: `systeminformation` as used by dotenvx/src/db/session.js (`system_uuid`, `os_platform`, `os_arch`).</remarks>
public static class SystemInformation
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
        string platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "darwin" : "Linux";
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

            foreach (string path in new[] { "/sys/class/dmi/id/product_uuid", "/etc/machine-id" })
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path).Trim().ToLowerInvariant();
                }
            }
        }
        catch (Exception)
        {
            // systeminformation reports an empty uuid when it cannot read one
        }

        return "";
    }
}
