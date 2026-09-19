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
}
