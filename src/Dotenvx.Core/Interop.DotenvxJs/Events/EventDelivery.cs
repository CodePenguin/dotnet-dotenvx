using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Events;

/// <summary>A place to POST batches of events (backends/armor.js).</summary>
internal interface IEventBackend
{
    /// <summary>Sends one batch; throws on failure (swallowed by the caller).</summary>
    void Send(JsonArray events);
}

/// <remarks>
/// Reference source: dotenvx/src/lib/events/backends/armor.js. Radar ingests client-reported activity at
/// POST /api/events. Authentication is captured before `run` loads application environment values.
/// </remarks>
internal sealed class ArmorEventsBackend(string hostname, string token) : IEventBackend
{
    /// <summary>Null when Armor is disabled, signed out or has no token.</summary>
    public static ArmorEventsBackend? Create(IReadOnlyDictionary<string, object?> options)
    {
        if (EventGate.Disabled(options))
        {
            return null;
        }

        var session = new Session();
        string? optionToken = EventGate.Token(options);
        if (optionToken is null && (!session.On() || session.Username() is null))
        {
            return null;
        }

        string? token = optionToken ?? session.Token();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return new ArmorEventsBackend(session.Hostname(), token);
    }

    public void Send(JsonArray events)
    {
        HttpResult response = ArmorHttp.Http($"{hostname}/api/events", "POST",
            new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}", ["Content-Type"] = "application/json" },
            ArmorWire.Stringify(new JsonObject { ["events"] = events }));

        // no response data belongs in console output (including errors)
        if (response.StatusCode >= 400)
        {
            throw new InvalidOperationException("Event delivery failed");
        }
    }
}

/// <summary>The Armor on/off/token rules shared by the background sink and the backend.</summary>
internal static class EventGate
{
    public static bool Disabled(IReadOnlyDictionary<string, object?> options)
    {
        return Environment.GetEnvironmentVariable("DOTENVX_NO_ARMOR") == "true" ||
               options.TryGetValue("armor", out object? armor) && armor is false ||
               options.TryGetValue("noArmor", out object? noArmor) && noArmor is true;
    }

    public static string? Token(IReadOnlyDictionary<string, object?> options)
    {
        return options.TryGetValue("token", out object? token) && token is string { Length: > 0 } text ? text : null;
    }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/events/delivery.js. In-process transport (SDK): a bounded queue pumped in batches with a
/// hard deadline; backend failures cannot escape this boundary.
/// </remarks>
internal sealed class EventDelivery : IEventSink
{
    private const int BatchSize = 50;
    private const int QueueLimit = 256;

    private readonly IEventBackend? _backend;
    private readonly int _timeoutMs;
    private readonly object _gate = new();
    private readonly List<JsonObject> _queue = [];
    private Task? _running;
    private Task? _closing;
    private bool _closed;

    public EventDelivery(IEventBackend? backend, int timeoutMs = 500)
    {
        _backend = backend;
        _timeoutMs = timeoutMs > 0 ? Math.Min(timeoutMs, 500) : 500;
        _closed = backend is null;
    }

    private void Stop()
    {
        lock (_gate)
        {
            _closed = true;
            _queue.Clear();
        }
    }

    private void Send(JsonArray batch)
    {
        try
        {
            Task send = Task.Run(() => _backend!.Send(batch));
            if (!send.Wait(_timeoutMs))
            {
                Stop();
            }
            else if (send.IsFaulted)
            {
                Stop();
            }
        }
        catch
        {
            Stop();
        }
    }

    private void Pump()
    {
        lock (_gate)
        {
            if (_closed || _running is not null || _queue.Count == 0)
            {
                return;
            }

            _running = Task.Run(() =>
            {
                while (true)
                {
                    JsonArray batch;
                    lock (_gate)
                    {
                        if (_closed || _queue.Count == 0)
                        {
                            break;
                        }

                        batch = [.. _queue.Take(BatchSize)];

                        _queue.RemoveRange(0, Math.Min(BatchSize, _queue.Count));
                    }

                    Send(batch);
                }

                lock (_gate)
                {
                    _running = null;
                }

                Pump();
            });
        }
    }

    public void Write(JsonObject @event, bool terminal = false)
    {
        lock (_gate)
        {
            if (_closed || _closing is not null)
            {
                return;
            }

            if (_queue.Count >= QueueLimit)
            {
                if (!terminal)
                {
                    return;
                }

                _queue.RemoveAt(_queue.Count - 1); // reserve room for the final result
            }

            _queue.Add(@event);
        }

        Pump();
    }

    public async Task Flush()
    {
        try
        {
            // overall flush deadline is separate from each batch's deadline
            Task all = Task.Run(async () =>
            {
                Pump();
                while (true)
                {
                    Task? current;
                    lock (_gate)
                    {
                        current = _running;
                    }

                    if (current is null)
                    {
                        break;
                    }

                    await current;
                }
            });
            if (await Task.WhenAny(all, Task.Delay(_timeoutMs)) != all)
            {
                Stop();
            }
        }
        catch
        {
            Stop();
        }
    }

    public Task Close()
    {
        lock (_gate)
        {
            return _closing ??= Flush().ContinueWith(_ => Stop(), TaskScheduler.Default);
        }
    }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/events/background.js. CLI delivery owns no network handles: the events are handed to a
/// detached `_deliver-events` worker over its stdin and credentials are read by the worker.
/// </remarks>
internal sealed class EventBackground : IEventSink
{
    private const int QueueLimit = 256;

    /// <summary>Starts the worker (swappable so unit tests never spawn processes): (stdin JSON, working directory, environment).</summary>
    public static Action<string, string, Dictionary<string, string>> Spawn { get; set; } = SpawnWorker;

    private readonly string? _token;
    private readonly Dictionary<string, string> _env;
    private readonly string _cwd;
    private readonly List<JsonObject> _queue = [];
    private Task? _closing;

    private EventBackground(string? token, Dictionary<string, string> env, string cwd)
    {
        _token = token;
        _env = env;
        _cwd = cwd;
    }

    /// <summary>Null when Armor is disabled or signed out (and no `--token` was given).</summary>
    public static EventBackground? Create(IReadOnlyDictionary<string, object?> options)
    {
        if (EventGate.Disabled(options))
        {
            return null;
        }

        var session = new Session();
        string? token = EventGate.Token(options);
        if (token is null && (!session.On() || session.Username() is null))
        {
            return null;
        }

        // captured before run loads application environment values
        return new EventBackground(token, WorkerEnvironment.Current(), Directory.GetCurrentDirectory());
    }

    public void Write(JsonObject @event, bool terminal = false)
    {
        if (_closing is not null)
        {
            return;
        }

        if (_queue.Count >= QueueLimit)
        {
            if (!terminal)
            {
                return;
            }

            _queue.RemoveAt(_queue.Count - 1);
        }

        _queue.Add(@event);
    }

    public Task Flush()
    {
        if (_queue.Count == 0)
        {
            return Task.CompletedTask;
        }

        JsonArray events = [];
        foreach (JsonObject item in _queue)
        {
            events.Add(item);
        }

        _queue.Clear();
        var options = new JsonObject();
        if (_token is not null)
        {
            options["token"] = _token;
        }

        string input = new JsonObject { ["options"] = options, ["events"] = events }.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        try
        {
            Spawn(input, _cwd, _env);
        }
        catch
        {
            // delivery is best effort
        }

        return Task.CompletedTask;
    }

    public Task Close()
    {
        return _closing ??= Flush();
    }

    private static void SpawnWorker(string input, string cwd, Dictionary<string, string> env)
    {
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("no process path");
        var info = new ProcessStartInfo(processPath)
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // `dotnet dotenvx.dll`: the host needs the entry assembly; a published apphost is the command itself
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            System.Reflection.Assembly.GetEntryAssembly()?.Location is { Length: > 0 } entry)
        {
            info.ArgumentList.Add(entry);
        }

        info.ArgumentList.Add("_deliver-events");
        info.Environment.Clear();
        foreach ((string key, string value) in env)
        {
            info.Environment[key] = value;
        }

        using Process child = Process.Start(info) ?? throw new InvalidOperationException("worker did not start");

        // only wait for the local pipe write, never worker startup or HTTP
        Task write = Task.Run(() =>
        {
            using var stdin = new StreamWriter(child.StandardInput.BaseStream, new UTF8Encoding(false));
            stdin.Write(input);
        });
        try
        {
            write.Wait(25);
        }
        catch
        {
            // the worker may already be gone
        }
    }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/events/worker.js (the hidden `_deliver-events` command). Internal, short-lived worker:
/// no output, no local logs, no retries.
/// </remarks>
internal static class EventWorker
{
    /// <summary>Reads {options, events} from the input and delivers them; always returns (the caller exits 0).</summary>
    public static void Run(TextReader input, Func<IReadOnlyDictionary<string, object?>, IEventBackend?>? createBackend = null)
    {
        try
        {
            var buffer = new StringBuilder();
            char[] chunk = new char[8192];
            int read;
            while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Append(chunk, 0, read);
                if (Encoding.UTF8.GetByteCount(buffer.ToString()) > 1024 * 1024)
                {
                    return;
                }
            }

            JsonNode? parsed = JsonNode.Parse(buffer.ToString());
            if (parsed?["events"] is not JsonArray { Count: <= 256 } events)
            {
                return;
            }

            var options = new Dictionary<string, object?>();
            if (parsed["options"] is JsonObject optionNode)
            {
                foreach ((string key, JsonNode? value) in optionNode)
                {
                    options[key] = value is JsonValue json && json.TryGetValue(out string? text) ? text :
                        value is JsonValue flag && flag.TryGetValue(out bool boolean) ? boolean : null;
                }
            }

            IEventBackend? backend = (createBackend ?? (o => ArmorEventsBackend.Create(o)))(options);
            if (backend is null)
            {
                return;
            }

            List<JsonNode?> all = [.. events];
            foreach (JsonNode?[] part in all.Chunk(50))
            {
                var batch = new JsonArray();
                foreach (JsonNode? item in part)
                {
                    batch.Add(item?.DeepClone());
                }

                Task send = Task.Run(() => backend.Send(batch));
                send.Wait(500);
            }
        }
        catch
        {
            // best effort
        }
    }
}
