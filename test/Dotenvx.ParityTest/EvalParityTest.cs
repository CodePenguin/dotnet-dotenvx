using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Locks in a behavior of the real CLI: a failing $() command substitution is silently
/// left unresolved by the real CLI (no error, exit 0), while other parse-time errors like decryption
/// failures do surface. If @dotenvx/primitives ever changes this again, these tests catch it.
/// </remarks>
public class EvalParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task SuccessfulCommandSubstitutionMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=\"$(echo world)\"\n"), null, "get");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task MultipleSubstitutionsInOneValueMatch()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "CMD=\"$(echo one),$(echo two)\"\n"), null, "get");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task FailingCommandIsSilentlyLeftUnresolvedWithExitZero()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "BAD=\"$(exit 1)\"\n"), null, "get");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Cross-implementation coverage: one CLI's `set --plain` writes the literal `$(...)` syntax
    /// to disk (set never evaluates it, only stores it verbatim), the other CLI's `get` performs
    /// the actual command substitution at read time - proving both the writer and the evaluator
    /// agree on this syntax, not just the evaluator alone (the earlier tests in this file only
    /// exercise one CLI's evaluator against a fixture neither CLI wrote).
    /// </remarks>
    [Fact]
    public async Task NodeSetsCommandSubstitutionValueThatDotnetEvaluates()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\n");
        CliResult set = await Fixture.RunNodeAsync(WorkDir, null, "set", "GREETING", "$(echo hi)", "--plain");
        set.ExitCode.ShouldBe(0);
        ReadFile(".env").ShouldContain("GREETING=\"$(echo hi)\"");

        CliResult get = await Fixture.RunDotnetAsync(WorkDir, null, "get", "GREETING");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("hi");
    }

    [Fact]
    public async Task DotnetSetsCommandSubstitutionValueThatNodeEvaluates()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\n");
        CliResult set = await Fixture.RunDotnetAsync(WorkDir, null, "set", "GREETING", "$(echo hi)", "--plain");
        set.ExitCode.ShouldBe(0);
        ReadFile(".env").ShouldContain("GREETING=\"$(echo hi)\"");

        CliResult get = await Fixture.RunNodeAsync(WorkDir, null, "get", "GREETING");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("hi");
    }
}
