using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Events;

/// <remarks>
/// Reference source: dotenvx/src/lib/events/*.js (upstream has no unit tests for the events subsystem; its behavior is proven
/// against the real Node CLI by Dotenvx.ParityTest.EventsParityTest).
/// </remarks>
public class EventsTest : EnvironmentTestBase
{
    private sealed class CapturingSink : IEventSink
    {
        public List<JsonObject> Events { get; } = [];

        public int Closed { get; private set; }

        public void Write(JsonObject @event, bool terminal = false)
        {
            Events.Add(@event);
        }

        public Task Flush()
        {
            return Task.CompletedTask;
        }

        public Task Close()
        {
            Closed++;

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingBackend : IEventBackend
    {
        public List<JsonArray> Batches { get; } = [];

        public Exception? Failure { get; init; }

        public int DelayMs { get; init; }

        /// <summary>The events delivered so far. Delivery gives up after 500 ms (like the Node SDK), so on a slow machine a send can
        /// still be running when a flush returns: read under the lock and wait for the expected count instead of assuming it is done.</summary>
        public JsonObject[] Delivered(int expected)
        {
            for (int attempt = 0; ; attempt++)
            {
                lock (Batches)
                {
                    JsonObject[] events = [.. Batches.SelectMany(batch => batch).Select(node => node!.AsObject())];
                    if (events.Length >= expected || attempt >= 100)
                    {
                        return events;
                    }
                }

                Thread.Sleep(100);
            }
        }

        public void Send(JsonArray events)
        {
            Thread.Sleep(DelayMs);
            lock (Batches)
            {
                Batches.Add(JsonNode.Parse(events.ToJsonString())!.AsArray());
            }

            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private static readonly Dictionary<string, object?> NoOptions = [];

    protected override void TearDown()
    {
        EventBackground.Spawn = (_, _, _) => { };
        EventSdk.CreateBackend = options => ArmorEventsBackend.Create(options);
        base.TearDown();
    }

    // ---- metadata

    [Fact]
    public void MetadataKeepsOnlyAllowlistedFieldsInAllowlistOrder()
    {
        JsonObject metadata = EventMetadata.Metadata(new Dictionary<string, object?>
        {
            ["exit_code"] = 1L,
            ["secret"] = "x",
            ["key"] = "HELLO",
            ["phase"] = "complete",
            ["token"] = "tok"
        });

        metadata.ToJsonString().ShouldBe("""{"key":"HELLO","phase":"complete","exit_code":1}""");
    }

    [Fact]
    public void OptionsExcludeTokensAndKeepAllowlistedOrder()
    {
        JsonObject options = EventMetadata.Options(new Dictionary<string, object?>
        {
            ["token"] = "tok",
            ["stdout"] = true,
            ["envFile"] = new List<string> { ".env", ".env.local" },
            ["armor"] = false
        });

        options.ToJsonString().ShouldBe("""{"envFile":[".env",".env.local"],"stdout":true,"armor":false}""");
    }

    [Fact]
    public void ValuesAreBounded()
    {
        JsonObject metadata = EventMetadata.Metadata(new Dictionary<string, object?>
        {
            ["key"] = new string('a', 2000),
            ["files"] = Enumerable.Range(0, 150).Select(index => (object?)$"f{index}").Append(new[] { "nested" }).ToList()
        });

        metadata["key"]!.GetValue<string>().Length.ShouldBe(1024);
        metadata["files"]!.AsArray().Count.ShouldBe(100);

        EventMetadata.Metadata(new Dictionary<string, object?> { ["files"] = new object?[] { "a", new[] { "nested" }, null } })
            .ToJsonString().ShouldBe("""{"files":["a",null]}""");
    }

    [Fact]
    public void ErrorCodesAreSanitised()
    {
        EventMetadata.ErrorCode(new DotenvxException("MISSING_KEY", "missing")).ShouldBe("MISSING_KEY");
        EventMetadata.ErrorCode(new Exception("ENOENT: no such file or directory, open 'x'")).ShouldBe("ENOENT");
        EventMetadata.ErrorCode(new Exception("boom")).ShouldBe("COMMAND_FAILED");
        EventMetadata.ErrorCode("lower_case").ShouldBe("COMMAND_FAILED");
        EventMetadata.ErrorCode("COMMAND_NOT_FOUND").ShouldBe("COMMAND_NOT_FOUND");
    }

    // ---- recorder

    [Fact]
    public async Task CompleteRecordsOneTerminalEventWithRuntimeAndOptions()
    {
        var sink = new CapturingSink();
        var events = new EventRecorder("cli/get", NoOptions,
            new Dictionary<string, object?> { ["strict"] = true, ["token"] = "tok" }, sink);

        events.Add(new Dictionary<string, object?> { ["key"] = "HELLO" });
        await events.Complete("success", new Dictionary<string, object?> { ["exit_code"] = 0L });
        await events.Complete("unsuccessful");

        sink.Events.Count.ShouldBe(1);
        sink.Closed.ShouldBe(1);
        JsonObject @event = sink.Events[0];
        @event["name"]!.GetValue<string>().ShouldBe("cli/get");
        @event["outcome"]!.GetValue<string>().ShouldBe("success");
        @event["occurred_at"]!.GetValue<string>().ShouldMatch(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$");
        JsonObject metadata = @event["metadata"]!.AsObject();
        metadata.Select(pair => pair.Key).ShouldBe(["key", "phase", "exit_code", "duration_ms", "options", "cli_version"]);
        metadata["options"]!.ToJsonString().ShouldBe("""{"strict":true}""");
        metadata["cli_version"]!.GetValue<string>().ShouldBe(PackageJson.Version);
    }

    [Fact]
    public async Task FailureMakesTheOutcomeUnsuccessfulAndRecordsTheErrorCode()
    {
        var sink = new CapturingSink();
        var events = new EventRecorder("cli/run", NoOptions, null, sink);

        events.Fail(new DotenvxException("MISSING_ENV_FILE", "missing"));
        await events.Complete();

        sink.Events[0]["outcome"]!.GetValue<string>().ShouldBe("unsuccessful");
        sink.Events[0]["metadata"]!["error_code"]!.GetValue<string>().ShouldBe("MISSING_ENV_FILE");
    }

    [Fact]
    public async Task SdkEventsCarrySdkRuntimeInsteadOfCliVersion()
    {
        var sink = new CapturingSink();
        var events = new EventRecorder("sdk/get", NoOptions, null, sink);

        await events.Complete();

        JsonObject metadata = sink.Events[0]["metadata"]!.AsObject();
        metadata["sdk_version"]!.GetValue<string>().ShouldBe(PackageJson.Version);
        metadata["sdk_language"]!.GetValue<string>().ShouldBe("javascript");
        metadata.ContainsKey("cli_version").ShouldBeFalse();
    }

    [Fact]
    public async Task FileRowsCountErrorsAndSurviveUntilComplete()
    {
        var sink = new CapturingSink();
        var events = new EventRecorder("sdk/set", NoOptions, null, sink);

        events.Add(new Dictionary<string, object?> { ["key"] = "A" });
        events.File(".env", true, null, new Dictionary<string, object?> { ["key"] = "A", ["output"] = "file" });
        events.File(".env.x", false, new Exception("boom"));
        await events.Complete();
        events.Record(); // after completion nothing more is written

        sink.Events.Count.ShouldBe(3);
        sink.Events[0]["metadata"]!.ToJsonString().ShouldContain("""{"key":"A","file":".env","changed":true,"output":"file","phase":"file"}""".TrimEnd('}'));
        sink.Events[1]["outcome"]!.GetValue<string>().ShouldBe("unsuccessful");
        sink.Events[1]["metadata"]!["error_code"]!.GetValue<string>().ShouldBe("COMMAND_FAILED");
        sink.Events[2]["outcome"]!.GetValue<string>().ShouldBe("unsuccessful");
        sink.Events[2]["metadata"]!["error_count"]!.GetValue<long>().ShouldBe(1);
    }

    // ---- background (CLI)

    [Theory]
    [InlineData("armor", false)]
    [InlineData("noArmor", true)]
    public void BackgroundIsOffWhenArmorIsDisabledByOption(string option, object value)
    {
        EventBackground.Create(new Dictionary<string, object?> { [option] = value, ["token"] = "tok" }).ShouldBeNull();
    }

    [Fact]
    public void BackgroundIsOffWhenSignedOutAndOnWithAToken()
    {
        EventBackground.Create(NoOptions).ShouldBeNull();
        EventBackground.Create(new Dictionary<string, object?> { ["token"] = "tok" }).ShouldNotBeNull();

        Environment.SetEnvironmentVariable("DOTENVX_NO_ARMOR", "true");
        EventBackground.Create(new Dictionary<string, object?> { ["token"] = "tok" }).ShouldBeNull();
    }

    [Fact]
    public async Task BackgroundHandsQueuedEventsToTheWorkerOnce()
    {
        var spawned = new List<(string Input, string Cwd, Dictionary<string, string> Env)>();
        EventBackground.Spawn = (input, cwd, env) => spawned.Add((input, cwd, env));
        Environment.SetEnvironmentVariable("SECRET_FOR_APP", "nope");

        EventBackground sink = EventBackground.Create(new Dictionary<string, object?> { ["token"] = "tok" })!;
        sink.Write(new JsonObject { ["name"] = "cli/get" });
        await sink.Close();
        await sink.Close();
        sink.Write(new JsonObject { ["name"] = "late" });
        await sink.Flush();

        spawned.Count.ShouldBe(1);
        spawned[0].Input.ShouldBe("""{"options":{"token":"tok"},"events":[{"name":"cli/get"}]}""");
        spawned[0].Env.Keys.ShouldNotContain("SECRET_FOR_APP");
        spawned[0].Env.Keys.ShouldContain("DOTENVX_CONFIG");
    }

    [Fact]
    public async Task BackgroundQueueIsBoundedAndKeepsRoomForTheTerminalEvent()
    {
        string? input = null;
        EventBackground.Spawn = (text, _, _) => input = text;
        EventBackground sink = EventBackground.Create(new Dictionary<string, object?> { ["token"] = "t" })!;

        for (int index = 0; index < 300; index++)
        {
            sink.Write(new JsonObject { ["i"] = index });
        }

        sink.Write(new JsonObject { ["i"] = "last" }, true);
        await sink.Close();

        JsonArray events = JsonNode.Parse(input!)!["events"]!.AsArray();
        events.Count.ShouldBe(256);
        events[255]!["i"]!.GetValue<string>().ShouldBe("last");
    }

    // ---- delivery (SDK)

    [Fact]
    public async Task DeliverySendsQueuedEventsInBatchesOfFifty()
    {
        var backend = new CapturingBackend();
        var delivery = new EventDelivery(backend);

        for (int index = 0; index < 120; index++)
        {
            delivery.Write(new JsonObject { ["i"] = index });
        }

        await delivery.Close();

        backend.Batches.Sum(batch => batch.Count).ShouldBeLessThanOrEqualTo(120);
        backend.Batches.ShouldAllBe(batch => batch.Count <= 50);
        backend.Batches.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DeliverySwallowsBackendFailuresAndStops()
    {
        var backend = new CapturingBackend { Failure = new InvalidOperationException("down") };
        var delivery = new EventDelivery(backend);

        delivery.Write(new JsonObject { ["i"] = 1 });
        await delivery.Close();
        delivery.Write(new JsonObject { ["i"] = 2 });
        await delivery.Close();

        backend.Batches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeliveryWithoutABackendIsInert()
    {
        var delivery = new EventDelivery(null);

        delivery.Write([]);
        await delivery.Close();
    }

    [Fact]
    public async Task DeliveryGivesUpOnASlowBackend()
    {
        var backend = new CapturingBackend { DelayMs = 3000 };
        var delivery = new EventDelivery(backend, 100);

        delivery.Write(new JsonObject { ["i"] = 1 });
        DateTime started = DateTime.UtcNow;
        await delivery.Close();

        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    // ---- worker

    [Fact]
    public void WorkerSendsEventsInBatchesOfFifty()
    {
        var backend = new CapturingBackend();
        string events = string.Join(',', Enumerable.Range(0, 120).Select(index => $$"""{"i":{{index}}}"""));

        EventWorker.Run(new StringReader($$"""{"options":{"token":"tok"},"events":[{{events}}]}"""), options =>
        {
            options["token"].ShouldBe("tok");

            return backend;
        });

        backend.Batches.Select(batch => batch.Count).ShouldBe([50, 50, 20]);
    }

    [Fact]
    public void WorkerIgnoresBadInputAndMissingBackends()
    {
        var backend = new CapturingBackend();
        EventWorker.Run(new StringReader("not json"), _ => backend);
        EventWorker.Run(new StringReader("""{"events":"nope"}"""), _ => backend);
        EventWorker.Run(new StringReader($$"""{"events":[{{string.Join(',', Enumerable.Repeat("{}", 257))}}]}"""), _ => backend);
        EventWorker.Run(new StringReader("""{"events":[{}]}"""), _ => null);

        backend.Batches.ShouldBeEmpty();
    }

    // ---- worker environment

    [Fact]
    public void WorkerEnvironmentPassesOnlyAccountAndNetworkSettings()
    {
        Dictionary<string, string> env = WorkerEnvironment.Filter(new Dictionary<string, string>
        {
            ["Path"] = "p",
            ["DOTENVX_CONFIG"] = "c",
            ["https_proxy"] = "h",
            ["DATABASE_URL"] = "secret",
            ["DOTENVX_NO_ARMOR"] = "true",
            ["DOTENV_PRIVATE_KEY"] = "k"
        });

        env.Keys.OrderBy(key => key, StringComparer.Ordinal).ShouldBe(["DOTENVX_CONFIG", "DOTENVX_NO_ARMOR", "Path", "https_proxy"]);
    }

    // ---- SDK

    [Fact]
    public async Task SdkGetAndSetReportOneEventEach()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotenvx-sdk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string envFile = Path.Combine(directory, ".env");
        File.WriteAllText(envFile, "HELLO=World\n");
        var backend = new CapturingBackend();
        EventSdk.CreateBackend = _ => backend;

        EventSdk sdk = Main.WithEvents();
        object? value = sdk.Get("HELLO", new GetOptions { Path = [envFile], NoNative = true });
        sdk.Set("A", "1", new SetsOptions { Path = [envFile], Plain = true, NoNative = true });
        await sdk.FlushEvents();

        value.ShouldBe("World");
        File.ReadAllText(envFile).ShouldContain("A=\"1\"");
        JsonObject[] events = [.. backend.Delivered(3).OrderBy(@event => @event["name"]!.GetValue<string>())];
        events.Select(@event => @event["name"]!.GetValue<string>()).ShouldBe(["sdk/get", "sdk/set", "sdk/set"]);

        JsonObject get = events[0]["metadata"]!.AsObject();
        get["key"]!.GetValue<string>().ShouldBe("HELLO");
        get["error_count"]!.GetValue<long>().ShouldBe(0);
        get["phase"]!.GetValue<string>().ShouldBe("complete");
        get["options"]!.ToJsonString().ShouldContain("\"noNative\":true");

        events.Where(@event => @event["name"]!.GetValue<string>() == "sdk/set")
            .Select(@event => @event["metadata"]!["phase"]!.GetValue<string>()).Order().ShouldBe(["complete", "file"]);
    }

    [Fact]
    public async Task SdkFailuresStillThrowAndAreReported()
    {
        var backend = new CapturingBackend();
        EventSdk.CreateBackend = _ => backend;
        EventSdk sdk = Main.WithEvents();

        Should.Throw<DotenvxException>(() => sdk.Get("X", new GetOptions { Path = ["missing-dir/.env"], Strict = true, NoNative = true }));
        await sdk.FlushEvents();

        JsonObject @event = backend.Delivered(1).Single();
        @event["outcome"]!.GetValue<string>().ShouldBe("unsuccessful");
        @event["metadata"]!["error_code"]!.GetValue<string>().ShouldBe("MISSING_ENV_FILE");
    }
}
