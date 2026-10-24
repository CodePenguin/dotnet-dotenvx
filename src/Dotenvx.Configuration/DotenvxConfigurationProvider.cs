using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace Dotenvx.Configuration;

public class DotenvxConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly List<IDisposable> _watchers = [];
    private readonly DotenvxConfigurationSource _source;

    public DotenvxConfigurationProvider(DotenvxConfigurationSource source)
    {
        _source = source;
        if (source.ReloadOnChange)
        {
            Watch();
        }
    }

    public void Dispose()
    {
        foreach (IDisposable watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        GC.SuppressFinalize(this);
    }

    public override void Load()
    {
        Data = Read();
    }

    private Dictionary<string, string?> Read()
    {
        ConfigOptions options = new();
        _source.ConfigureOptions?.Invoke(options);

        // Nothing is written to the console or to the process environment: the provider only reads.
        options.Logger ??= new SilentDotenvxLogger();
        options.ProcessEnv ??= Snapshot();

        Dictionary<string, string?> data = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in Dotenvx.Config(options).Parsed)
        {
            // Microsoft's environment variable convention: a double underscore is the hierarchy separator
            data[pair.Key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)] = pair.Value;
        }

        return data;
    }

    private static Dictionary<string, string> Snapshot()
    {
        Dictionary<string, string> snapshot = [];
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            snapshot[(string)entry.Key] = (string?)entry.Value ?? "";
        }

        return snapshot;
    }

    private void Watch()
    {
        ConfigOptions options = new();
        _source.ConfigureOptions?.Invoke(options);
        List<string> paths = options.Path ??
                             (options.PathString?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                 .ToList() ?? [".env"]);

        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (directory is null || !Directory.Exists(directory))
            {
                continue;
            }

            // the default filter hides dot-prefixed files, which is every .env file
            PhysicalFileProvider files = new(directory, ExclusionFilters.None);
            string name = Path.GetFileName(fullPath);
            _watchers.Add(files);
            _watchers.Add(ChangeToken.OnChange(() => files.Watch(name), Reload));
        }
    }

    private void Reload()
    {
        Thread.Sleep(_source.ReloadDelay);
        try
        {
            Data = Read();
        }
        catch (Exception)
        {
            // keep the last good values, as the file providers do when a reload fails
            return;
        }

        OnReload();
    }
}
