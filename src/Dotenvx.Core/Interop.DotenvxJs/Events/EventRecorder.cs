using System.Diagnostics;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Events;

/// <summary>Where recorded events go (delivery.js / background.js).</summary>
public interface IEventSink
{
    void Write(JsonObject @event, bool terminal = false);

    Task Flush();

    Task Close();
}

/// <remarks>
/// Reference source: dotenvx/src/lib/events/index.js (`createEvents`). Records one operation's events: `file` rows, free-form
/// `add`ed details, a failure, and the terminal `complete` event, all filtered through <see cref="EventMetadata" />.
/// </remarks>
public sealed class EventRecorder
{
    private readonly string _name;
    private readonly IReadOnlyDictionary<string, object?> _options;
    private readonly IReadOnlyDictionary<string, object?> _selectedOptions;
    private readonly IEventSink _sink;
    private readonly Stopwatch _started = Stopwatch.StartNew();
    private Dictionary<string, object?> _details = [];
    private Task? _completion;
    private bool _failed;

    /// <param name="name">The operation (`cli/run`, `sdk/get`, ...).</param>
    /// <param name="options">The command/SDK options (`stdout` decides the `output` of file rows).</param>
    /// <param name="eventOptions">The options reported in the event (`config.eventOptions || options`).</param>
    /// <param name="sink">Null: events are dropped (no Armor credentials, or disabled).</param>
    public EventRecorder(string name, IReadOnlyDictionary<string, object?> options,
        IReadOnlyDictionary<string, object?>? eventOptions, IEventSink? sink)
    {
        _name = name;
        _options = options;
        _selectedOptions = ToDictionary(EventMetadata.Options(eventOptions ?? options));
        _sink = sink ?? NullSink.Instance;
    }

    public Task Flush()
    {
        return _sink.Flush();
    }

    private static Dictionary<string, object?> ToDictionary(JsonObject node)
    {
        return node.ToDictionary(pair => pair.Key, pair => Unwrap(pair.Value));
    }

    private static object? Unwrap(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonArray array => array.Select(Unwrap).ToList(),
            JsonValue value when value.TryGetValue(out bool flag) => flag,
            JsonValue value when value.TryGetValue(out string? text) => text,
            JsonValue value when value.TryGetValue(out long number) => number,
            JsonValue value when value.TryGetValue(out double number) => number,
            _ => null
        };
    }

    public void Record(IReadOnlyDictionary<string, object?>? metadata = null, string outcome = "success", bool terminal = false)
    {
        try
        {
            if (_completion is not null)
            {
                return;
            }

            JsonObject eventOptions = EventMetadata.Options(_selectedOptions);
            JsonObject merged = EventMetadata.Metadata(metadata ?? new Dictionary<string, object?>());
            if (eventOptions.Count > 0)
            {
                merged["options"] = eventOptions;
            }

            if (_name.StartsWith("sdk/", StringComparison.Ordinal))
            {
                merged["sdk_version"] = PackageJson.Version;
                merged["sdk_language"] = "javascript";
            }
            else
            {
                merged["cli_version"] = PackageJson.Version;
            }

            _sink.Write(new JsonObject
            {
                ["name"] = _name,
                ["occurred_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                ["outcome"] = outcome == "success" ? "success" : "unsuccessful",
                ["metadata"] = merged
            }, terminal);
        }
        catch
        {
            // events never affect the operation
        }
    }

    /// <summary>A per-file row (SDK set).</summary>
    public void File(string? envFilepath, bool changed, Exception? error, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        try
        {
            if (_completion is not null)
            {
                return;
            }

            _details["error_count"] = (_details.TryGetValue("error_count", out object? count) && count is long previous ? previous : 0) +
                                      (error is not null ? 1 : 0);
            if (error is not null)
            {
                _failed = true;
            }

            var row = new Dictionary<string, object?>
            {
                ["output"] = _options.TryGetValue("stdout", out object? stdout) && stdout is true ? "stdout" : "file"
            };
            if (_details.TryGetValue("key", out object? key))
            {
                row["key"] = key;
            }

            foreach ((string name, object? value) in metadata ?? new Dictionary<string, object?>())
            {
                row[name] = value;
            }

            row["phase"] = "file";
            row["file"] = envFilepath;
            row["changed"] = error is null && changed;
            if (error is not null)
            {
                row["error_code"] = EventMetadata.ErrorCode(error);
            }

            Record(row, error is not null ? "unsuccessful" : "success");
        }
        catch
        {
            // events never affect the operation
        }
    }

    public void Add(IReadOnlyDictionary<string, object?> metadata)
    {
        try
        {
            JsonObject filtered = EventMetadata.Metadata(metadata);
            var next = new Dictionary<string, object?>(_details);
            foreach ((string key, JsonNode? value) in filtered)
            {
                next[key] = Unwrap(value);
            }

            _details = next;
        }
        catch
        {
            // events never affect the operation
        }
    }

    public void Fail(Exception error)
    {
        _failed = true;
        try
        {
            _details["error_code"] = EventMetadata.ErrorCode(error);
        }
        catch
        {
            // events never affect the operation
        }
    }

    /// <summary>`fail` for a plain `{ code }` error (executeCommand's `COMMAND_NOT_FOUND`).</summary>
    public void Fail(string? code)
    {
        _failed = true;
        _details["error_code"] = EventMetadata.ErrorCode(code);
    }

    public Task Complete(string? outcome = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        if (_completion is not null)
        {
            return _completion;
        }

        var all = new Dictionary<string, object?>(_details);
        foreach ((string key, object? value) in metadata ?? new Dictionary<string, object?>())
        {
            all[key] = value;
        }

        all["phase"] = "complete";
        all["duration_ms"] = (long)Math.Round(_started.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
        Record(all, outcome ?? (_failed ? "unsuccessful" : "success"), true);

        return _completion = _sink.Close();
    }

    private sealed class NullSink : IEventSink
    {
        public static readonly NullSink Instance = new();

        public void Write(JsonObject @event, bool terminal = false)
        {
        }

        public Task Flush()
        {
            return Task.CompletedTask;
        }

        public Task Close()
        {
            return Task.CompletedTask;
        }
    }
}
