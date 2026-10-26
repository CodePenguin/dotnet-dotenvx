using System.Globalization;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Db;

/// <remarks>
/// Reference source: dotenvx/src/db/session.js. The session is a `.env`-formatted settings file in the config directory
/// (`DOTENVX_CONFIG` or the OS default): the hostname, username, user id and on/off switch live there, the access token
/// in the OS secret store when it works (else the same file). Like upstream, the store is only opened when the file
/// already exists, so read-only commands never create it.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class Session(IDotenvxLogger? logger = null)
{
    private const long Hours24 = 60L * 60 * 24 * 1000;
    private const string VersionUrl = "https://dotenvx.sh/VERSION";

    public const string ArmorHostname = "DOTENVX_ARMOR_HOSTNAME";
    public const string ArmorUser = "DOTENVX_ARMOR_USER";
    public const string ArmorUsername = "DOTENVX_ARMOR_USERNAME";
    public const string ArmorToken = "DOTENVX_ARMOR_TOKEN";
    public const string ArmorOn = "DOTENVX_ARMOR_ON";
    public const string DotenvxVersion = "DOTENVX_VERSION";
    public const string DotenvxVersionLastCheck = "DOTENVX_VERSION_LAST_CHECK";

    /// <summary>Test seam: `Date.now()`.</summary>
    public static Func<long> NowMilliseconds { get; internal set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    internal static void ResetToDefaults()
    {
        NowMilliseconds = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private ConfStore? _store;

    private string ConfigPath()
    {
        string? cwd = Environment.GetEnvironmentVariable("DOTENVX_CONFIG");

        return Path.GetFullPath(Path.Combine(string.IsNullOrEmpty(cwd) ? EnvPaths.Config("dotenvx") : cwd, ".env"));
    }

    private bool ConfigExists()
    {
        return File.Exists(ConfigPath());
    }

    private static ConfStore NewStore()
    {
        return new ConfStore(Environment.GetEnvironmentVariable("DOTENVX_CONFIG"), ".env",
            serialize: JsonToEnv, deserialize: DotenvParse);
    }

    /// <summary>jsonToEnv.js: `KEY="value"` lines (values are not escaped).</summary>
    public static string JsonToEnv(Dictionary<string, object> json)
    {
        return string.Join("\n", json.Select(entry =>
            entry.Key + "=" + $"\"{Convert.ToString(entry.Value, CultureInfo.InvariantCulture)}\""));
    }

    private static Dictionary<string, object> DotenvParse(string env)
    {
        var parsed = new Dictionary<string, object>();
        foreach (Primitives.ScanEntry entry in Primitives.Primitives.Scan(env))
        {
            parsed[entry.Key] = entry.Value;
        }

        return parsed;
    }

    public virtual ConfStore CreateStore()
    {
        return _store ??= NewStore();
    }

    public virtual ConfStore? OpenStore()
    {
        if (_store is null && !ConfigExists())
        {
            return null;
        }

        return _store ??= NewStore();
    }

    public virtual string Status()
    {
        // if logged in
        return !string.IsNullOrEmpty(Username()) && !string.IsNullOrEmpty(Token()) && On() ? "on" : "off";
    }

    private string? ReadSetting(string key)
    {
        return OpenStore()?.GetString(key);
    }

    public virtual string Hostname()
    {
        string? value = ReadSetting(ArmorHostname);

        return string.IsNullOrEmpty(value) ? "https://armor.dotenvx.com" : value;
    }

    public virtual string? Username()
    {
        string? value = ReadSetting(ArmorUsername);

        return string.IsNullOrEmpty(value) ? null : value;
    }

    public virtual string? Token()
    {
        string? value = GetFromSecretStore(ArmorToken);

        return string.IsNullOrEmpty(value) ? null : value;
    }

    public virtual bool On()
    {
        string? value = ReadSetting(ArmorOn);

        return (string.IsNullOrEmpty(value) ? "true" : value) == "true";
    }

    public virtual bool Off()
    {
        string? value = ReadSetting(ArmorOn);

        return (string.IsNullOrEmpty(value) ? "true" : value) == "false";
    }

    public virtual string DevicePublicKey()
    {
        return new Device().PublicKey();
    }

    public virtual string StorePath()
    {
        return _store is not null ? _store.Path : ConfigPath();
    }

    public virtual SystemInformation.Info SystemInformation()
    {
        return Helpers.SystemInformation.Current();
    }

    private static string NormalizeVersion(string? version)
    {
        string trimmed = (version ?? "").Trim();

        return trimmed.StartsWith('v') ? trimmed[1..] : trimmed;
    }

    private static int[] ParseVersion(string? version)
    {
        return [.. NormalizeVersion(version).Split('.').Select(part =>
        {
            int digits = 0;
            while (digits < part.Length && (char.IsDigit(part[digits]) || (digits == 0 && part[digits] is '+' or '-')))
            {
                digits++;
            }

            return int.TryParse(part[..digits], out int number) ? number : 0;
        })];
    }

    internal static bool VersionGreaterThan(string left, string right)
    {
        int[] leftParts = ParseVersion(left);
        int[] rightParts = ParseVersion(right);

        for (int i = 0; i < Math.Max(leftParts.Length, rightParts.Length); i++)
        {
            int leftPart = i < leftParts.Length ? leftParts[i] : 0;
            int rightPart = i < rightParts.Length ? rightParts[i] : 0;

            if (leftPart > rightPart)
            {
                return true;
            }

            if (leftPart < rightPart)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Checks at most once a day whether a newer dotenvx exists (only when the settings file exists).</summary>
    public virtual void NotifyUpdate()
    {
        try
        {
            ConfStore? store = OpenStore();
            if (store is null)
            {
                return;
            }

            logger?.Debug("checking if update available");

            double.TryParse(store.GetString(DotenvxVersionLastCheck), NumberStyles.Float, CultureInfo.InvariantCulture,
                out double parsedLastCheck);
            long lastCheck = (long)parsedLastCheck;
            long now = NowMilliseconds();

            if (lastCheck + Hours24 >= now)
            {
                return;
            }

            string remote = PackageJson.Version;

            try
            {
                logger?.Debug("fetching latest available dotenvx version");
                HttpResult response = ArmorHttp.Http(VersionUrl, "GET", null, null);
                remote = NormalizeVersion(response.Body);
                logger?.Debug($"latest dotenvx version: {remote}");
                store.Set(DotenvxVersion, remote);
            }
            catch (Exception error)
            {
                logger?.Debug(error.Message);
            }

            store.Set(DotenvxVersionLastCheck, now);

            if (VersionGreaterThan(remote, PackageJson.Version))
            {
                Console.Error.WriteLine("⛆ update available [curl -sfS https://dotenvx.sh | sh]");
            }
        }
        catch (Exception error)
        {
            logger?.Debug(error.Message);
        }
    }

    /// <summary>`noArmor()` / `noArmorSync()`: DOTENVX_NO_ARMOR=true or no active session.</summary>
    public virtual bool NoArmor()
    {
        if (Environment.GetEnvironmentVariable("DOTENVX_NO_ARMOR") == "true")
        {
            logger?.Debug("armor: off");

            return true;
        }

        string status = Status();
        logger?.Debug($"armor: {status}");

        return status == "off";
    }

    public virtual string? GetFromSecretStore(string key)
    {
        try
        {
            string? value = NativeStore.Get(key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        catch
        {
            // session.js: `catch {}`
        }

        return OpenStore()?.GetString(key);
    }

    public virtual string SetInSecretStore(string key, string value)
    {
        try
        {
            NativeStore.Set(key, value);

            return value;
        }
        catch
        {
            CreateStore().Set(key, value);

            return value;
        }
    }

    public virtual void DeleteFromSecretStore(string key)
    {
        try
        {
            NativeStore.Delete(key);
        }
        catch
        {
            // session.js: `catch {}`
        }

        OpenStore()?.Delete(key);
    }

    public virtual string TurnOn()
    {
        CreateStore().Set(ArmorOn, "true");

        return "true";
    }

    public virtual string TurnOff()
    {
        CreateStore().Set(ArmorOn, "false");

        return "false";
    }

    public virtual string Login(string? hostname, string? id, string? username, string? accessToken)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_HOSTNAME not set. Run [dotenvx armor login]");
        }

        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_USER not set. Run [dotenvx armor login]");
        }

        if (string.IsNullOrEmpty(username))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_USERNAME not set. Run [dotenvx armor login]");
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_TOKEN not set. Run [dotenvx armor login]");
        }

        ConfStore store = CreateStore();
        store.Set(ArmorUser, id);
        store.Set(ArmorUsername, username);
        SetInSecretStore(ArmorToken, accessToken);
        store.Set(ArmorHostname, hostname);
        store.Set(ArmorOn, "true");

        return accessToken;
    }

    public virtual bool Logout(string? hostname, string? id, string? accessToken)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_HOSTNAME not set. Run [dotenvx armor login]");
        }

        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_USER not set. Run [dotenvx armor login]");
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("DOTENVX_ARMOR_TOKEN not set. Run [dotenvx armor login]");
        }

        DeleteFromSecretStore(ArmorToken);

        ConfStore? store = OpenStore();
        if (store is null)
        {
            return true;
        }

        store.Delete(ArmorUser);
        store.Delete(ArmorUsername);
        store.Delete(ArmorHostname);
        store.Delete(ArmorOn);
        store.Delete(DotenvxVersion);
        store.Delete(DotenvxVersionLastCheck);

        return true;
    }
}
