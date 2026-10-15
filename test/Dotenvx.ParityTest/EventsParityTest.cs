using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - end-to-end comparison of the events subsystem (dotenvx/src/lib/events/*, `_deliver-events`) against a
/// fake Armor API: the POST /api/events requests that each tracked command sends (timestamps and durations normalised) must be
/// byte-identical between the Node and the .NET CLI.
/// </remarks>
public class EventsParityTest(NodeParityFixture fixture) : ArmorParityTestBase(fixture)
{
    private string[] Events(Outcome outcome)
    {
        return [.. outcome.Requests.Where(request => request.Contains("POST /api/events"))];
    }

    [Theory]
    [InlineData("get", "HELLO")]
    [InlineData("get")]
    [InlineData("get", "HELLO", "--format", "shell", "-pp")]
    [InlineData("get", "--pp", "--strict", "-o")]
    [InlineData("get", "HELLO", "-f", ".env", "--env-keys-file", ".env.keys", "--no-native")]
    [InlineData("get", "HELLO", "-fk", "a.keys", "-fk", "b.keys")]
    [InlineData("get", "HELLO", "--mask", "--ignore", "A", "B", "-ik", "X", "-ek", "Y", "Z")]
    [InlineData("get", "HELLO", "-q")]
    [InlineData("get", "MISSING", "-f", "missing.env")]
    [InlineData("keypair")]
    [InlineData("keypair", "DOTENV_PUBLIC_KEY")]
    [InlineData("keypair", "NOPE")]
    [InlineData("decrypt", "--stdout")]
    [InlineData("decrypt", "-k", "HELLO", "-ek", "B")]
    [InlineData("encrypt", "--stdout")]
    [InlineData("encrypt")]
    [InlineData("set", "NEW", "1")]
    [InlineData("set", "NEW", "1", "--plain", "--no-create", "-fk", "k.keys")]
    [InlineData("del", "HELLO")]
    [InlineData("del", "HELLO", "-f", "nope.env")]
    [InlineData("run", "--", "node", "-e", "process.exit(0)")]
    [InlineData("run", "--", "node", "-e", "process.exit(3)")]
    [InlineData("run", "--strict", "--redact", "--", "node", "-e", "1")]
    [InlineData("run", "-f", ".env,.env.other", "--convention", "nextjs", "--", "node", "-e", "1")]
    [InlineData("run", "--", "definitely-not-a-command-xyz")]
    [InlineData("protect", "--docker")]
    [InlineData("protect")]
    [InlineData("protect", "--git-process")]
    [InlineData("protect", "--git-file", "x.env")]
    [InlineData("set", "NOVALUE")]
    public async Task TrackedCommandEventsMatchNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, "{}");
        RedactFiles = true;
        RedactRequests = true;
        CompareStderr = false;

        Outcome outcome = await CompareWith(() => WriteFile(".env", FixedKeypairFixture.EncryptedEnv), null,
            null, args);

        Events(outcome).Length.ShouldBe(1, "tracked commands send one terminal event; requests: " + string.Join(" | ", outcome.Requests) + " stderr: " + outcome.Stderr);
        Events(outcome)[0].ShouldContain($"\"name\":\"cli/{(args[0] is "enc" ? "encrypt" : args[0])}\"");
    }

    [Theory]
    [InlineData("--no-armor")]
    [InlineData("--strict")]
    public async Task EventsAreOnlySentWhileSignedInLikeNode(string option)
    {
        if (!Available)
        {
            return;
        }

        // signed out: no session, so no events
        RedactFiles = true;
        RedactRequests = true;
        CompareStderr = false;
        Outcome outcome = await CompareWith(() => WriteFile(".env", FixedKeypairFixture.EncryptedEnv), null,
            null, "get", "HELLO", option);

        Events(outcome).ShouldBeEmpty();
    }

    [Fact]
    public async Task NoArmorOptionAndEnvironmentSuppressEventsLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        CompareStderr = false;
        Outcome flag = await CompareWith(() => WriteFile(".env", FixedKeypairFixture.EncryptedEnv), null,
            null, "get", "HELLO", "--no-armor");
        Events(flag).ShouldBeEmpty();

        Outcome environment = await CompareWith(() => WriteFile(".env", FixedKeypairFixture.EncryptedEnv), null,
            new Dictionary<string, string> { ["DOTENVX_NO_ARMOR"] = "true" }, "get", "HELLO");
        Events(environment).ShouldBeEmpty();
    }

    [Fact]
    public async Task TokenOptionSendsEventsEvenWhenArmorIsOffLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn(on: false);
        CompareStderr = false;
        Outcome outcome = await CompareWith(() => WriteFile(".env", FixedKeypairFixture.EncryptedEnv), null,
            null, "run", "--token", "tok_x", "--", "node", "-e", "1");

        Events(outcome).Length.ShouldBe(1);
        Events(outcome)[0].ShouldContain("Bearer tok_x");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    [InlineData(302)]
    public async Task FailingDeliveryNeverAffectsTheCommandLikeNode(int status)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(status, "{}");
        RedactFiles = true;
        RedactRequests = true;
        Outcome outcome = await CompareWith(() => WriteFile(".env", "A=1\n"), null, null, "get", "A");

        outcome.ExitCode.ShouldBe(0);
        Events(outcome).Length.ShouldBe(1);
    }

    [Fact]
    public async Task SlowDeliveryIsAbandonedAfterTheTimeoutLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ =>
        {
            Thread.Sleep(1500);

            return new FakeResponse(200, "{}");
        };
        RedactFiles = true;
        RedactRequests = true;
        Outcome outcome = await CompareWith(() => WriteFile(".env", "A=1\n"), null, null, "get", "A");

        outcome.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task UnreachableServerNeverAffectsTheCommandLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Dispose();
        CompareStderr = false;
        Outcome outcome = await CompareWith(() => WriteFile(".env", "A=1\n"), null,
            null, "get", "A");

        outcome.ExitCode.ShouldBe(0);
    }

    // worker.js: events are posted in batches of 50, and a payload of more than 256 events is dropped without a single request
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(100)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(300)]
    public async Task DeliverEventsWorkerBatchesAndLimitsLikeNode(int count)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, "{}");
        CompareStderr = false;
        string events = string.Join(",", Enumerable.Range(0, count).Select(index => $"{{\"name\":\"cli/get\",\"n\":{index}}}"));
        string input = $"{{\"options\":{{\"token\":\"tok_x\"}},\"events\":[{events}]}}";

        Outcome outcome = await CompareWith(null, input, null, "_deliver-events");

        Events(outcome).Length.ShouldBe(count is 0 or > 256 ? 0 : (count + 49) / 50);
    }
}
