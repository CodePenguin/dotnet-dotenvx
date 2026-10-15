using System.Collections;

namespace Dotenvx.Interop.DotenvxJs.Events;

/// <remarks>
/// Reference source: dotenvx/src/lib/events/workerEnvironment.js. Only account discovery, native credential-store access and
/// network settings reach the delivery worker; application secrets and runtime injection flags never do.
/// </remarks>
public static class WorkerEnvironment
{
    private static readonly HashSet<string> Allowed =
    [
        "DOTENVX_CONFIG", "DOTENVX_NO_ARMOR",
        "HOME", "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA",
        "XDG_CONFIG_HOME", "XDG_DATA_HOME", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS",
        "PATH", "PATHEXT", "SYSTEMROOT", "WINDIR", "TMPDIR", "TMP", "TEMP", "LANG", "LC_ALL",
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
        "NODE_EXTRA_CA_CERTS", "NODE_USE_SYSTEM_CA", "SSL_CERT_FILE", "SSL_CERT_DIR"
    ];

    public static Dictionary<string, string> Filter(IEnumerable<KeyValuePair<string, string>> env)
    {
        var result = new Dictionary<string, string>();
        foreach ((string key, string value) in env)
        {
            if (Allowed.Contains(key.ToUpperInvariant()))
            {
                result[key] = value;
            }
        }

        return result;
    }

    public static Dictionary<string, string> Current()
    {
        return Filter(Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Select(entry => new KeyValuePair<string, string>((string)entry.Key, (string?)entry.Value ?? "")));
    }
}
