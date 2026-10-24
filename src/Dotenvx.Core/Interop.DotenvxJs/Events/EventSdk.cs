using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx;

/// <remarks>
/// Reference source: dotenvx/src/lib/events/sdk.js. An explicit, scoped SDK facade: calls report an `sdk/&lt;name&gt;` event to
/// Armor when connected. Calls stay synchronous; applications can flush completed calls before shutdown with
/// <see cref="FlushEvents" /> without any global hooks.
/// </remarks>
public sealed class EventSdk
{
    private readonly object _gate = new();
    private readonly HashSet<Task> _pending = [];

    /// <summary>Delivery backend factory (swappable so unit tests never touch the network).</summary>
    internal static Func<IReadOnlyDictionary<string, object?>, IEventBackend?> CreateBackend { get; set; } =
        options => ArmorEventsBackend.Create(options);

    public ConfigResult Config(ConfigOptions? options = null)
    {
        return Invoke("config", ToEventOptions(options), [],
            events => Main.Config(options, events));
    }

    public object? Get(string? key, GetOptions? options = null)
    {
        return Invoke("get", ToEventOptions(options), new Dictionary<string, object?> { ["key"] = key },
            events => Main.Get(key, options, events));
    }

    public SetsResult Set(string key, string value, SetsOptions? options = null)
    {
        return Invoke("set", ToEventOptions(options), new Dictionary<string, object?> { ["key"] = key },
            events => Main.Set(key, value, options, events));
    }

    /// <summary>Flush event delivery for calls that have completed.</summary>
    public async Task FlushEvents()
    {
        Task[] pending;
        lock (_gate)
        {
            pending = [.. _pending];
        }

        await Task.WhenAll(pending);
    }

    private T Invoke<T>(string name, Dictionary<string, object?> options, Dictionary<string, object?> metadata,
        Func<EventRecorder, T> action)
    {
        EventRecorder events;
        try
        {
            IEventBackend? backend = null;
            try
            {
                backend = EventMetadata.Catalog.ContainsKey($"sdk/{name}") ? CreateBackend(options) : null;
            }
            catch
            {
                // missing credentials, keychain failures and setup are optional
            }

            events = new EventRecorder($"sdk/{name}", options, null, new EventDelivery(backend));
        }
        catch
        {
            events = new EventRecorder($"sdk/{name}", options, null, null);
        }

        events.Add(metadata);
        try
        {
            T result = action(events);
            Complete(events);

            return result;
        }
        catch (Exception error)
        {
            events.Fail(error);
            Complete(events);

            throw;
        }
    }

    private void Complete(EventRecorder events)
    {
        Task completion = events.Complete().ContinueWith(_ => { }, TaskScheduler.Default);
        lock (_gate)
        {
            _pending.Add(completion);
        }

        completion.ContinueWith(_ =>
        {
            lock (_gate)
            {
                _pending.Remove(completion);
            }
        }, TaskScheduler.Default);
    }

    /// <summary>The SDK option names events report (metadata.js option allowlist), for the options that were set.</summary>
    private static Dictionary<string, object?> ToEventOptions(ConfigOptions? options)
    {
        var result = new Dictionary<string, object?>();
        if (options is null)
        {
            return result;
        }

        void Set(string name, object? value)
        {
            if (value is not null)
            {
                result[name] = value;
            }
        }

        Set("path", options.Path is { } path ? path.ToList() : options.PathString);
        Set("overload", options.Overload);
        Set("ignore", options.Ignore);
        Set("strict", options.Strict);
        Set("envKeysFile", options.EnvKeysFile);
        Set("noNative", options.NoNative);
        Set("noArmor", options.NoArmor);
        Set("token", options.Token);
        Set("no1Password", options.No1Password);
        Set("noBitwarden", options.NoBitwarden);
        Set("convention", options.Convention);
        Set("debug", options.Debug);
        Set("quiet", options.Quiet);
        Set("verbose", options.Verbose);
        Set("logLevel", options.LogLevel);
        Set("mask", options.Mask);
        if (options is GetOptions get)
        {
            Set("all", get.All);
            Set("format", get.Format);
        }

        if (options is SetsOptions sets)
        {
            Set("create", sets.NoCreate is true ? false : null);
            Set("plain", sets.Plain);
            Set("encrypt", sets.Encrypt);
        }

        return result;
    }
}
