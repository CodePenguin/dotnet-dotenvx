using Dotenvx.Primitives;

namespace Dotenvx.Providers;

/// <remarks>
/// Reference source: @dotenvx/providers settings.js. dotenvx keeps its settings (Armor token and host, the 1Password and
/// Bitwarden references of stored keys) in `.env` inside the config directory (`DOTENVX_CONFIG`, else the OS default). A
/// missing file is "no setting"; any other read failure is `SETTINGS_FAILED`.
/// </remarks>
internal static class ProviderSettings
{
    internal static string FilePath(ProviderOptions options)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? configured = options.Env("DOTENVX_CONFIG");
        string directory;
        if (!string.IsNullOrEmpty(configured))
        {
            directory = configured;
        }
        else if (OperatingSystem.IsMacOS())
        {
            directory = Path.Combine(home, "Library", "Preferences", "dotenvx");
        }
        else if (OperatingSystem.IsWindows())
        {
            string? appData = options.Env("APPDATA");
            directory = Path.Combine(string.IsNullOrEmpty(appData) ? Path.Combine(home, "AppData", "Roaming") : appData,
                "dotenvx", "Config");
        }
        else
        {
            string? xdg = options.Env("XDG_CONFIG_HOME");
            directory = Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "dotenvx");
        }

        return Path.GetFullPath(Path.Combine(directory, ".env"));
    }

    private static string? Value(string source, string key)
    {
        List<ScanEntry> entries = Primitives.Primitives.Scan(source);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Key == key)
            {
                return entries[i].Value;
            }
        }

        return null;
    }

    public static string? GetSync(string key, ProviderOptions options)
    {
        try
        {
            return Value(File.ReadAllText(FilePath(options)), key);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception)
        {
            throw new ProviderException("SETTINGS_FAILED", "could not read dotenvx settings");
        }
    }

    public static Task<string?> Get(string key, ProviderOptions options)
    {
        return Task.FromResult(GetSync(key, options));
    }
}
