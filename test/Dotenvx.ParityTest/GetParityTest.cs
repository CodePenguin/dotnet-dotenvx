using Shouldly;

namespace Dotenvx.ParityTest;

public class GetParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task GetReturnsIdenticalJsonForAllKeys()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\n"), null, "get");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task GetSingleKeyReturnsIdenticalValue()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("eval")]
    public async Task GetFormatFlagsProduceIdenticalOutput(string format)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\n"), null, "get", "--format", format);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task GetMissingKeyMatchesExitCodeAndStdout()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        // stderr wording is a known, pre-existing, accepted cosmetic difference (tracked
        // separately) - this asserts the part of the contract that actually matters: does the
        // command signal success/failure and print the same thing a script would consume.
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "NOTFOUND");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Found a real bug here: JsonSerializerOptions.WriteIndented defaults to Environment.NewLine
    /// ("\r\n" on Windows) for its line breaks, while Node's JSON.stringify always emits "\n" -
    /// fixed by setting NewLine = "\n" explicitly (see GetAction.cs, KeypairAction.cs, LsAction.cs).
    /// </remarks>
    [Fact]
    public async Task PrettyPrintFormatMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\n"), null, "get", "--pretty-print");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task GetMissingKeyWithIgnoreFlagExitsZero()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "NOTFOUND", "--ignore", "MISSING_KEY");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    // --file and DOTENV_FILE (commit 5a28c859) were forward-ported ahead of our v2.24.1 pin - the
    // pinned Node CLI genuinely doesn't support them yet ("error: unknown option '--file'"), so
    // there's no valid parity comparison for them until the pin advances past that commit. See
    // .claude/plan.md. DOTENV_PATH/DOTENV_F (already in the pin) are covered in
    // NormalizeDotenvConfigPathTest.cs at the unit level instead.
}
