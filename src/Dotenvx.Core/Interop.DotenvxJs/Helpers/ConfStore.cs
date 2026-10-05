using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Port of `env-paths@2.2.1` (`envPaths(name, { suffix: '' }).config`), bundled in @dotenvx/tooling.
/// </remarks>
public static class EnvPaths
{
    public static string Config(string name)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Preferences", name);
        }

        if (OperatingSystem.IsWindows())
        {
            string? appData = Environment.GetEnvironmentVariable("APPDATA");
            string root = string.IsNullOrEmpty(appData) ? Path.Combine(home, "AppData", "Roaming") : appData;

            return Path.Combine(root, name, "Config");
        }

        string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, name);
    }
}

/// <remarks>
/// Port of the slice of `conf@10.2` (bundled in @dotenvx/tooling) that dotenvx's `db/device.js` and `db/session.js` use: a
/// flat key/value file in the user config directory (or `DOTENVX_CONFIG`), optionally AES-256-CBC encrypted
/// (`iv(16) ':' ciphertext`, key = pbkdf2-sha512(password, iv.toString(), 10000, 32)), with optional custom
/// (de)serializers. Behaviors kept on purpose: the directory is created on any read of a missing file, an invalid file
/// reads as empty (`clearInvalidConfig`), and keys are flat (dotenvx never uses dotted keys, so conf's dot-prop
/// nesting is not ported).
/// </remarks>
public class ConfStore
{
    private readonly Func<string, Dictionary<string, object>>? _deserialize;
    private readonly string? _encryptionKey;
    private readonly Func<Dictionary<string, object>, string>? _serialize;

    public ConfStore(string? cwd, string configName, string? encryptionKey = null,
        Func<Dictionary<string, object>, string>? serialize = null,
        Func<string, Dictionary<string, object>>? deserialize = null)
    {
        Path = string.IsNullOrEmpty(cwd)
            ? System.IO.Path.Combine(EnvPaths.Config("dotenvx"), configName)
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(cwd, configName));
        _encryptionKey = encryptionKey;
        _serialize = serialize;
        _deserialize = deserialize;
    }

    public string Path { get; }

    private Dictionary<string, object> Store
    {
        get
        {
            byte[] data;
            try
            {
                data = Fsx.ReadFileSync(Path);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                // conf's `_ensureDirectory()` runs when the config file does not exist
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

                return new Dictionary<string, object>();
            }

            try
            {
                string text = _encryptionKey is null ? Encoding.UTF8.GetString(data) : Decrypt(data);

                return _deserialize is not null ? _deserialize(text) : JsonParse(text);
            }
            catch (Exception)
            {
                // clearInvalidConfig: an unreadable file is an empty store
                return new Dictionary<string, object>();
            }
        }
    }

    public object? Get(string key)
    {
        return Store.TryGetValue(key, out object? value) ? value : null;
    }

    public string? GetString(string key)
    {
        return Get(key) switch
        {
            null => null,
            string text => text,
            var other => Convert.ToString(other, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public void Set(string key, object value)
    {
        Dictionary<string, object> store = Store;
        store[key] = value;
        Write(store);
    }

    public void Delete(string key)
    {
        Dictionary<string, object> store = Store;
        store.Remove(key);
        Write(store);
    }

    private void Write(Dictionary<string, object> store)
    {
        string text = _serialize is not null ? _serialize(store) : JsonStringify(store);
        byte[] data = _encryptionKey is null ? Encoding.UTF8.GetBytes(text) : Encrypt(text);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string temp = Path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllBytes(temp, data);
        File.Move(temp, Path, true);
    }

    /// <summary>`JSON.stringify(value, undefined, '\t')` for a flat string/number map.</summary>
    private static string JsonStringify(Dictionary<string, object> store)
    {
        if (store.Count == 0)
        {
            return "{}";
        }

        IEnumerable<string> lines = store.Select(entry =>
            $"\t{JsonSerializer.Serialize(entry.Key, JsonOptions)}: {JsonSerializer.Serialize(entry.Value, JsonOptions)}");

        return "{\n" + string.Join(",\n", lines) + "\n}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static Dictionary<string, object> JsonParse(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        var result = new Dictionary<string, object>();

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString()!,
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => property.Value.GetRawText()
            };
        }

        return result;
    }

    private byte[] DeriveKey(byte[] iv)
    {
        // `crypto.pbkdf2Sync(key, initializationVector.toString(), 10000, 32, 'sha512')`: the salt is the IV decoded as UTF-8
        // (invalid bytes become U+FFFD), then encoded again
        byte[] salt = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(iv));

        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(_encryptionKey!), salt, 10000,
            HashAlgorithmName.SHA512, 32);
    }

    private byte[] Encrypt(string text)
    {
        byte[] iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = DeriveKey(iv);
        byte[] cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(text), iv, PaddingMode.PKCS7);

        return [.. iv, (byte)':', .. cipher];
    }

    private string Decrypt(byte[] data)
    {
        byte[] iv = data[..16];
        using var aes = Aes.Create();
        aes.Key = DeriveKey(iv);

        return Encoding.UTF8.GetString(aes.DecryptCbc(data[17..], iv, PaddingMode.PKCS7));
    }
}
