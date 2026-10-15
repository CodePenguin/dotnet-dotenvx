using System.CommandLine;
using System.Text.Json.Nodes;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/tests/cli/actions/feedback.test.js. Upstream stubs PostFeedback and the prompt; here the HTTP
/// seam answers the request and the working directory holds the session settings. The terminal prompt scenarios ("prompts
/// in a terminal and supports hostname override", "cancellation exits without submitting") need a real TTY on stdin and are
/// covered by the node-vs-.NET comparison instead.
/// </remarks>
public class FeedbackActionTest : EnvironmentTestBase
{
    private readonly List<(string Url, string Method, string? Body)> _requests = [];

    public FeedbackActionTest()
    {
        PackageJson.Version = "0.0.0-test";
        string config = Environment.GetEnvironmentVariable("DOTENVX_CONFIG")!;
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ".env"), "DOTENVX_ARMOR_HOSTNAME=\"https://example.com\"\n");
    }

    private void Respond(Func<(int Status, string Body)> handler)
    {
        ArmorHttp.Request = (url, method, _, body) =>
        {
            _requests.Add((url, method, body));
            (int status, string text) = handler();

            return new HttpResult(status, text);
        };
    }

    private int Run(params string[] args)
    {
        RootCommand root = new LoggingRootCommand { new FeedbackAction(MockLogger) };

        return root.Parse(args).Invoke();
    }

    /// <remarks>Refer to "sends the supplied message to the configured hostname"</remarks>
    [Fact]
    public void TestSendsTheSuppliedMessageToTheConfiguredHostname()
    {
        Respond(() => (200, """{"ok":true}"""));

        Run("feedback", "Please improve this error").ShouldBe(0);

        _requests.Count.ShouldBe(1);
        _requests[0].Url.ShouldBe("https://example.com/api/feedback");
        JsonNode.Parse(_requests[0].Body!)!["body"]!.ToString().ShouldBe("Please improve this error");
        MockLogger.InfoMessages.ShouldContain("✔ Feedback sent, thanks!");
    }

    /// <remarks>Refer to "prompts in a terminal and supports hostname override" (the hostname override half)</remarks>
    [Fact]
    public void TestHostnameOverride()
    {
        Respond(() => (200, """{"ok":true}"""));

        Run("feedback", "A suggestion", "--hostname", "https://custom.example.com").ShouldBe(0);

        _requests[0].Url.ShouldBe("https://custom.example.com/api/feedback");
        JsonNode.Parse(_requests[0].Body!)!["body"]!.ToString().ShouldBe("A suggestion");
    }

    /// <remarks>Refer to "rejects blank feedback without making a request"</remarks>
    [Fact]
    public void TestRejectsBlankFeedbackWithoutMakingARequest()
    {
        Respond(() => (200, "{}"));

        Run("feedback", " \n ").ShouldBe(1);

        _requests.ShouldBeEmpty();
        MockLogger.ErrorMessages.ShouldContain("feedback cannot be empty");
    }

    /// <remarks>Refer to "missing noninteractive input fails without prompting" (stdin is redirected under the test host)</remarks>
    [Fact]
    public void TestMissingNoninteractiveInputFailsWithoutPrompting()
    {
        Console.IsInputRedirected.ShouldBeTrue();
        Respond(() => (200, "{}"));

        Run("feedback").ShouldBe(1);

        _requests.ShouldBeEmpty();
        MockLogger.ErrorMessages.ShouldContain("provide a message: dotenvx feedback \"Your feedback\"");
    }

    /// <remarks>Refer to "request errors fail without a success message"</remarks>
    [Fact]
    public void TestRequestErrorsFailWithoutASuccessMessage()
    {
        Respond(() => (503, """{"error":{"code":"UNAVAILABLE","message":"Service unavailable"}}"""));

        Run("feedback", "A suggestion").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(m => m.Contains("Service unavailable"));
        MockLogger.InfoMessages.ShouldNotContain(m => m.Contains("Feedback sent"));
    }

    /// <remarks>
    /// Refer to "feedback is listed only in the hidden menu". The root help omits the hidden command (the exact help bytes
    /// are compared by HelpExactParityTest); here the command itself is hidden and described.
    /// </remarks>
    [Fact]
    public void TestFeedbackIsListedOnlyInTheHiddenMenu()
    {
        FeedbackAction action = new(MockLogger);

        action.Hidden.ShouldBeTrue();
        action.Description.ShouldBe("send feedback to dotenvx");
    }

    // Intentionally NOT ported upstream tests (approved by the user, 2026-10-15); kept as skipped tests so the upstream-test-titles
    // scan sees them and the reason is visible here. Add a new one only after asking.
    /// <remarks>Refer to "prompts in a terminal and supports hostname override"</remarks>
    [Fact(Skip = "Not ported: Both need a real TTY on stdin (process.stdin.isTTY = true), which the test host cannot provide. Their cases are compared against the real Node CLI instead. (the hostname half is `FeedbackActionTest.TestHostnameOverride`)")]
    public void PromptsInATerminalAndSupportsHostnameOverride()
    {
    }

    /// <remarks>Refer to "cancellation exits without submitting"</remarks>
    [Fact(Skip = "Not ported: Both need a real TTY on stdin (process.stdin.isTTY = true), which the test host cannot provide. Their cases are compared against the real Node CLI instead.")]
    public void CancellationExitsWithoutSubmitting()
    {
    }

}
