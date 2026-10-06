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

    /// <remarks>
    /// Regression test for a 2026-09-28 bug: `--version`/`-V` is only registered on the root
    /// command by default (matching how commander's own `--version` is a program-level, but still
    /// recursive, option) - without `Recursive = true` on .NET's `VersionOption`,
    /// `dotenvx get --version` fell through to the `[KEY]` positional argument instead of printing
    /// the version, exiting 1 with `[MISSING_KEY] missing key (--version)` where Node prints the
    /// version and exits 0.
    /// </remarks>
    [Fact]
    public async Task VersionFlagAfterSubcommandIsRecognizedOnBothSides()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "--version");

        dotnet.ExitCode.ShouldBe(0);
        node.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Locks in the versioning policy established 2026-10-02 (see docs/porting-traps.md, submodule-pin note):
    /// `src/Dotenvx/Dotenvx.csproj`'s `Version` is kept in lockstep with the submodule's pinned tag,
    /// so `dotenvx --version` truthfully states which upstream release this port is at parity with -
    /// not just "both sides print something", but the literal same string. A prior version of this
    /// suite (see the now-removed doc comment on `VersionFlagAfterSubcommandIsRecognizedOnBothSides`)
    /// deliberately didn't compare the string because the policy wasn't settled yet.
    /// </remarks>
    [Fact]
    public async Task RootVersionFlagMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "--version");

        dotnet.ExitCode.ShouldBe(0);
        node.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
