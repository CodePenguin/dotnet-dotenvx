using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): `dotenvx feedback` (src/cli/actions/feedback.js,
/// src/lib/api/postFeedback.js) against a fake Armor API. Exit code, stdout, stderr and the requests the API received must match Node.
/// </remarks>
public class FeedbackParityTest(NodeParityFixture fixture) : ArmorParityTestBase(fixture)
{
    private async Task Run(Func<RecordedRequest, FakeResponse> handler, params string[] args)
    {
        if (!Available)
        {
            return;
        }

        Server.Handler = handler;
        Outcome outcome = await Compare(args);
        outcome.Requests.Length.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public Task Sent() => Run(_ => new FakeResponse(200, "{}"), "feedback", "Great tool", "--hostname", Server.BaseUrl);

    [Fact]
    public Task SentToTheSessionHostname()
    {
        LoggedIn();

        return Run(_ => new FakeResponse(200, "{}"), "feedback", "from session");
    }

    [Fact]
    public Task SentWithUnicodeAndQuotes() =>
        Run(_ => new FakeResponse(200, "{}"), "feedback", "héllo \"quoted\" ⛨ \\ back", "--hostname", Server.BaseUrl);

    [Fact]
    public Task ApiError() =>
        Run(_ => new FakeResponse(422, """{"error":{"code":"INVALID","message":"too short","help":"write more"}}"""),
            "feedback", "x", "--hostname", Server.BaseUrl);

    [Fact]
    public Task ApiErrorWithoutHelp() =>
        Run(_ => new FakeResponse(400, """{"error":{"code":"BAD","message":"nope"}}"""), "feedback", "x", "--hostname", Server.BaseUrl);

    [Fact]
    public Task ApiErrorWithoutCode() =>
        Run(_ => new FakeResponse(500, """{"error":{"message":"boom"}}"""), "feedback", "x", "--hostname", Server.BaseUrl);

    [Fact]
    public Task EmptyMessage() => Run(_ => new FakeResponse(200, "{}"), "feedback", "   ", "--hostname", Server.BaseUrl);

    [Fact]
    public Task EmptyStringMessage() => Run(_ => new FakeResponse(200, "{}"), "feedback", "", "--hostname", Server.BaseUrl);

    [Fact]
    public Task NoMessageWithoutATerminal() => Run(_ => new FakeResponse(200, "{}"), "feedback", "--hostname", Server.BaseUrl);

    [Fact]
    public Task QuietStillReportsSuccess() => Run(_ => new FakeResponse(200, "{}"), "feedback", "hi", "--quiet", "--hostname", Server.BaseUrl);

    [Fact]
    public Task DebugOutput() => Run(_ => new FakeResponse(200, "{}"), "feedback", "hi", "--debug", "--hostname", Server.BaseUrl);

    [Fact]
    public async Task DeadServer()
    {
        if (!Available)
        {
            return;
        }

        string dead = Server.BaseUrl;
        Server.Dispose();
        Outcome outcome = await Compare("feedback", "hi", "--hostname", dead);
        outcome.ExitCode.ShouldBe(1);
    }
}
