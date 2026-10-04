using Dotenvx.Interop.DotenvxJs.Shared;

using NSubstitute;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/executeCommand.test.js. The upstream tests stub `execa`; these run
/// real child processes (FakeGitleaks, built next to this assembly, writes to stdout/stderr and exits with a chosen
/// code). Ported: "executeCommand - success", "executeCommand - exitCode 1", "executeCommand - command-does-not-exist",
/// "executeCommand - ENOENT" and "executeCommand - redacts stdout and stderr even when attached to a TTY" (here: the
/// redacting pipe path). Not ported, because this port deliberately has no pty and only SIGINT handling (see
/// ExecuteCommand.cs's class remark): "executeCommand - uses a PTY when redacting an interactive terminal",
/// "executeCommand - error with OTHER signal", "executeCommand - command failed error", "executeCommand - sigintHandler",
/// "executeCommand - first SIGINT in TTY mode is not forwarded", "executeCommand - first SIGINT in TTY mode does not
/// suppress non-signal errors", "executeCommand - SIGINT second press escalates to SIGTERM then SIGKILL in TTY mode",
/// "executeCommand - SIGINT forwards in non-TTY mode", "executeCommand - queued SIGKILL is skipped if child is already
/// exiting", "executeCommand - queued SIGINT forward is skipped if child is already exiting in non-TTY mode",
/// "executeCommand - SIGTERM forwards in non-TTY mode", "executeCommand - forwards non-SIGINT/SIGTERM signals
/// immediately", "executeCommand - sigtermHandler".
/// </remarks>
public class ExecuteCommandTest
{
    private static readonly string FakeGitleaksDllPath = Path.Combine(AppContext.BaseDirectory, "FakeGitleaks.dll");

    private static string[] Fake(int exitCode, params string[] ops)
    {
        return ["dotnet", FakeGitleaksDllPath, exitCode.ToString(), .. ops];
    }

    /// <remarks>Refer to "executeCommand - success"</remarks>
    [Fact]
    public async Task TestSuccess()
    {
        IDotenvxLogger logger = Substitute.For<IDotenvxLogger>();

        int exitCode = await ExecuteCommand(logger, Fake(0), new Dictionary<string, string> { ["HELLO"] = "World" });

        exitCode.ShouldBe(0);
        logger.DidNotReceiveWithAnyArgs().Error(default!);
    }

    /// <remarks>Refer to "executeCommand - exitCode 1"</remarks>
    [Fact]
    public async Task TestExitCode1()
    {
        IDotenvxLogger logger = Substitute.For<IDotenvxLogger>();

        int exitCode = await ExecuteCommand(logger, Fake(1), new Dictionary<string, string>());

        exitCode.ShouldBe(1);
        // a plain nonzero exit from the wrapped command is never logged by dotenvx itself
        logger.DidNotReceiveWithAnyArgs().Error(default!);
    }

    /// <remarks>
    /// Refer to "executeCommand - command-does-not-exist" and "executeCommand - ENOENT": an unresolvable command is
    /// reported as `Unknown command: X`. On Windows real Node instead re-runs it through %COMSPEC% (cross-spawn's
    /// shell fallback, which this port now reproduces, see ExecuteCommand.cs and trap #33), so cmd.exe's own
    /// "is not recognized" text reaches stderr, our logger stays silent, and the exit code is cmd's (1).
    /// </remarks>
    [Fact]
    public async Task TestCommandDoesNotExist()
    {
        IDotenvxLogger logger = Substitute.For<IDotenvxLogger>();

        int exitCode = await ExecuteCommand(logger, ["definitely-not-a-real-command-xyz"],
            new Dictionary<string, string>());

        exitCode.ShouldBe(1);
        if (OperatingSystem.IsWindows())
        {
            logger.DidNotReceiveWithAnyArgs().Error(default!);
        }
        else
        {
            logger.Received().Error("Unknown command: definitely-not-a-real-command-xyz");
        }
    }

    /// <remarks>
    /// Refer to "executeCommand - redacts stdout and stderr even when attached to a TTY". Upstream asserts the stdio
    /// wiring (`['inherit', 'pipe', 'pipe']`) and the data interception; here the observable result is checked: a
    /// sensitive value printed to either stream comes out redacted.
    /// </remarks>
    [Fact]
    public async Task TestRedactsStdoutAndStderr()
    {
        IDotenvxLogger logger = Substitute.For<IDotenvxLogger>();
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter capturedOut = new();
        StringWriter capturedError = new();
        Console.SetOut(capturedOut);
        Console.SetError(capturedError);
        try
        {
            int exitCode = await ExecuteCommand(logger, Fake(0, "O:token=secret", "E:oops=secret"),
                new Dictionary<string, string> { ["HELLO"] = "secret" }, ["secret"]);

            exitCode.ShouldBe(0);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        capturedOut.ToString().ShouldContain("token=[REDACTED]");
        capturedOut.ToString().ShouldNotContain("secret");
        capturedError.ToString().ShouldContain("oops=[REDACTED]");
        capturedError.ToString().ShouldNotContain("secret");
    }
}
