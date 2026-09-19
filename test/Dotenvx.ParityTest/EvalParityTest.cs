using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Locks in the exact discovery from this session: a failing $() command substitution is silently
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
}
