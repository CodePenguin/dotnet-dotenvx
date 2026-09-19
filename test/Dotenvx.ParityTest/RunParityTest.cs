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
}
