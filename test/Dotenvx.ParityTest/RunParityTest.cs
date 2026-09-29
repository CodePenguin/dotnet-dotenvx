using Shouldly;

namespace Dotenvx.ParityTest;

public class RunParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task InjectedEnvIsVisibleToTheChildProcessIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        // node is used as the child command since it's the one executable guaranteed present by
        // this fixture, and is real (not a shell builtin) on every OS - see the nested-command
        // expansion fix earlier this session for why a shell builtin like `echo` isn't a safe
        // choice here.
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null,
            "run", "--", "node", "-e", "console.log(process.env.HELLO)");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task NestedDashDashArgumentsAreNotExpandedInEitherCli()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        // Regression check for the fix in ec21e34: only the outer executable should be resolved
        // via `which`; a literal argument that happens to match an executable name after a nested
        // -- must pass through unchanged.
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null,
            "run", "--", "node", "-e", "console.log(process.argv[1])", "--", "node");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task MissingSeparatorErrorMatchesExitCode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(() => WriteFile(".env", "HELLO=World\n"), null, "run");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
    }

    /// <remarks>
    /// Cross-implementation coverage for the actual "drop-in replacement" promise: one CLI's `set`
    /// creates a fresh encrypted value against the shared fixed keypair, the other CLI's `run`
    /// decrypts it and injects it into a real child process - proving `run`'s decrypt-and-inject
    /// path, not just `get`'s, interoperates.
    /// </remarks>
    [Fact]
    public async Task NodeSetsEncryptedValueThatDotnetRunInjectsDecrypted()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
        CliResult set = await Fixture.RunNodeAsync(WorkDir, null, "set", "GREETING", "hello-from-node");
        set.ExitCode.ShouldBe(0);

        CliResult run = await Fixture.RunDotnetAsync(WorkDir, null,
            "run", "--", "node", "-e", "console.log(process.env.GREETING)");

        run.ExitCode.ShouldBe(0);
        run.StandardOutput.Trim().ShouldBe("hello-from-node");
    }

    [Fact]
    public async Task DotnetSetsEncryptedValueThatNodeRunInjectsDecrypted()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
        CliResult set = await Fixture.RunDotnetAsync(WorkDir, null, "set", "GREETING", "hello-from-dotnet");
        set.ExitCode.ShouldBe(0);

        CliResult run = await Fixture.RunNodeAsync(WorkDir, null,
            "run", "--", "node", "-e", "console.log(process.env.GREETING)");

        run.ExitCode.ShouldBe(0);
        run.StandardOutput.Trim().ShouldBe("hello-from-dotnet");
    }

    /// <remarks>
    /// Locks in the `--redact` fix (found via a flag-combination sweep, 2026-09-26 - see CLAUDE.md
    /// trap #31): the child process's OWN stdout must have the injected value replaced with
    /// [REDACTED], on both sides identically, while the process still exits 0 (the child genuinely
    /// received the real value in its environment - only what it PRINTS gets filtered).
    /// </remarks>
    [Fact]
    public async Task RedactHidesInjectedValueFromChildProcessOutputIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null,
            "run", "--redact", "--", "node", "-e", "console.log('value: ' + process.env.HELLO)");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldContain("[REDACTED]");
        dotnet.StandardOutput.ShouldNotContain("World");
    }

    /// <remarks>
    /// Refer to `dotenvx/src/lib/helpers/cryptography/isPlainKey.js`'s `_PLAIN` exemption -
    /// `redactedValues.js` skips any key ending in `_PLAIN` when building the sensitive-value set.
    /// </remarks>
    [Fact]
    public async Task RedactDoesNotHidePlainSuffixedKeyValueIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO_PLAIN=NotSecret\n"), null,
            "run", "--redact", "--", "node", "-e", "console.log(process.env.HELLO_PLAIN)");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldContain("NotSecret");
    }

    /// <remarks>
    /// Regression test for a 2026-09-29 bug found via a code audit: `RunAction.cs`'s gate was
    /// `commandArgs.Length &lt; 1 || !hasSeparator`, rejecting ANY `run` invocation without a
    /// literal `--` separator. Real `run.js` only gates on `commandArgs.length &lt; 1` -
    /// `hasSeparator` merely selects which error message to print when that gate fires. Node
    /// supports running a command WITHOUT `--` via `inferCommandArgsFromProcessArgv`'s fallback
    /// (skip recognized `-f`/flag tokens, return the first plausible non-flag token onward) - a
    /// bare `dotenvx run node -e "..."` works fine on real Node. The two existing C# unit tests for
    /// this exact scenario (`RunActionTest.TestRunEnvFileAmbiguousArgumentsMissingDoubleDash*`) had
    /// themselves been misported from the start - the real upstream JS tests they're named after
    /// assert SUCCESS, not the "ambiguous command" error (confirmed via `dotenvx/tests/cli/actions/
    /// run.test.js`), which is why this divergence went undetected until an explicit code audit.
    /// </remarks>
    [Fact]
    public async Task RunWithoutDoubleDashSeparatorStillRunsTheCommand()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null,
            "run", "node", "-e", "console.log(process.env.HELLO)");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
