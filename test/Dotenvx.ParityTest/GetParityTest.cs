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

    /// <remarks>
    /// Implements get's --include-key/-ik and --exclude-key/-ek, previously entirely unimplemented
    /// (a real gap, not just missing test coverage - see GetAction.cs's FilterKeys, mirroring
    /// Encrypt.cs's existing _include/_exclude Picomatch pattern).
    /// </remarks>
    [Fact]
    public async Task IncludeKeyFiltersToOnlyTheNamedKeys()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\nBAZ=qux\n"), null,
            "get", "--include-key", "FOO", "--include-key", "BAZ");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task ExcludeKeyOmitsTheNamedKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\nBAZ=qux\n"), null, "get", "--exclude-key", "FOO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task IncludeAndExcludeKeyCombineIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\nBAZ=qux\n"), null,
            "get", "--include-key", "FOO", "--include-key", "BAZ", "--exclude-key", "BAZ");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task IncludeKeyGlobPatternMatchesIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\nFOOBAR=baz\nBAZ=qux\n"), null,
            "get", "--include-key", "FOO*");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task OverloadFlagPrefersDotenvValueOverProcessEnvIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        Dictionary<string, string> env = new() { ["HELLO"] = "FromProcessEnv" };
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=FromDotenv\n"), env, "get", "HELLO", "--overload");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.Trim().ShouldBe("FromDotenv");
    }

    [Fact]
    public async Task WithoutOverloadProcessEnvTakesPrecedenceIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        Dictionary<string, string> env = new() { ["HELLO"] = "FromProcessEnv" };
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=FromDotenv\n"), env, "get", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.Trim().ShouldBe("FromProcessEnv");
    }

    /// <remarks>
    /// --all's output includes the whole machine environment, which isn't safe to diff
    /// byte-for-byte between a `node` process and a `dotnet` process (each runtime injects its
    /// own incidental env vars) - instead, confirm a distinctive marker var we control flows
    /// through identically on both sides.
    /// </remarks>
    [Fact]
    public async Task AllFlagIncludesMachineEnvVarsIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        Dictionary<string, string> env = new() { ["PARITY_ALL_MARKER"] = "marker-value" };
        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), env, "get", "PARITY_ALL_MARKER", "--all");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        node.StandardOutput.Trim().ShouldBe("marker-value");
        dotnet.StandardOutput.Trim().ShouldBe("marker-value");
    }

    [Fact]
    public async Task StrictFlagExitsNonZeroOnMissingKeyIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "NOTFOUND", "--strict");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public async Task InlineEnvOptionIsPickedUpIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "INLINE", "--env", "INLINE=inline-value");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.Trim().ShouldBe("inline-value");
    }

    // --file and DOTENV_FILE (commit 5a28c859) were forward-ported ahead of our v2.24.1 pin - the
    // pinned Node CLI genuinely doesn't support them yet ("error: unknown option '--file'"), so
    // there's no valid parity comparison for them until the pin advances past that commit. See
    // .claude/plan.md. DOTENV_PATH/DOTENV_F (already in the pin) are covered in
    // NormalizeDotenvConfigPathTest.cs at the unit level instead.
}
