using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Locks in a real bug this suite found: commander.js (Node) treats -l/-q/-v/-d as global options
/// usable before OR after the subcommand name. System.CommandLine only recognized them before the
/// subcommand unless explicitly marked Recursive - without that, a subcommand with an optional
/// positional argument (e.g. `get [KEY]`, `ls [directory]`) silently swallowed the flag token as
/// that argument's value instead of routing it to the log-level options (`dotnet get -v` printed
/// "[MISSING_KEY] missing -v key" instead of running `get` with no key at verbose level). Fixed by
/// setting Recursive = true on all four options in LoggingRootCommand.cs.
/// </remarks>
public class GlobalOptionsParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task VerboseFlagAfterSubcommandWithOptionalArgumentMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "-v");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task DebugFlagAfterLsSubcommandDoesNotBecomeTheDirectoryArgument()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "ls", "-d");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        // Debug output includes noisy internal details (timings, raw option dumps) that aren't
        // byte-for-byte tracked elsewhere in this suite either - what matters here is that the
        // flag was consumed as a log-level option, not as the `directory` argument, which the
        // exit code and the presence of tree output (not a "not found" error) already prove.
        dotnet.StandardOutput.ShouldContain(".env");
    }

    [Fact]
    public async Task QuietFlagAfterSubcommandSuppressesNonInfoOutputIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "del", "NOTFOUND", "-q");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
