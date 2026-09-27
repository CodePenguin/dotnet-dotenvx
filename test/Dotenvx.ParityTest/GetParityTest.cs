using System.Text;

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

    /// <remarks>
    /// Implements get's --mask flag (optional-value): default 6 visible chars when no value given.
    /// </remarks>
    [Fact]
    public async Task MaskFlagWithDefaultVisibleCharsMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=SuperSecretValue\nFOO=bar\n"), null, "get", "--mask");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task MaskFlagWithCustomVisibleCharsMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=SuperSecretValue\nFOO=bar\n"), null, "get", "--mask", "3");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Single-key lookup path (not the JSON-object path): confirmed live that `get KEY --mask`
    /// prints the masked single value, so masking must run before the key != null branch.
    /// </remarks>
    [Fact]
    public async Task MaskFlagWithSingleKeyLookupMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=SuperSecretValue\nFOO=bar\n"), null, "get", "HELLO", "--mask");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// filterKeys runs before mask (upstream get.js order), so --include-key narrows first, then
    /// the surviving keys get masked.
    /// </remarks>
    [Fact]
    public async Task MaskFlagCombinedWithIncludeKeyMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=SuperSecretValue\nFOO=bar\nBAZ=qux\n"), null,
            "get", "--include-key", "HELLO", "--mask");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    // --file and DOTENV_FILE (commit 5a28c859) were forward-ported ahead of our v2.24.1 pin - the
    // pinned Node CLI genuinely doesn't support them yet ("error: unknown option '--file'"), so
    // there's no valid parity comparison for them until the pin advances past that commit. See
    // .claude/plan.md. DOTENV_PATH/DOTENV_F (already in the pin) are covered in
    // NormalizeDotenvConfigPathTest.cs at the unit level instead.

    /// <remarks>
    /// Regression test for a 2026-09-27 bug: `DetectEncoding.cs` returns "utf16le" for a file
    /// starting with a UTF-16LE BOM, matching Node's own encoding name - but `Fsx.ReadFileX`'s
    /// encoding switch didn't recognize that literal string and threw
    /// (`'utf16le' is not a supported encoding name`) on every read of such a file.
    /// </remarks>
    [Fact]
    public async Task Utf16LeEncodedEnvFileMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => File.WriteAllText(Path.Combine(WorkDir, ".env"), "HELLO=World\n", Encoding.Unicode), null,
            "get", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Regression test for a 2026-09-26 bug: `--include-key`/`--exclude-key` are genuinely
    /// variadic upstream (`-ik, --include-key &lt;includeKeys...&gt;`), but `System.CommandLine`'s
    /// `Option&lt;T[]?&gt;` defaults `AllowMultipleArgumentsPerToken` to false, so
    /// `--include-key HELLO FOO` (space-separated, one flag occurrence) only captured "HELLO" into
    /// the array and let "FOO" fall through to `get`'s own `[KEY]` positional argument - silently
    /// switching into single-key mode and producing empty output instead of the filtered object.
    /// Fixed by setting `AllowMultipleArgumentsPerToken = true` (see CLAUDE.md's trap for the full
    /// list of options this affected across the whole CLI, not just `get`).
    /// </remarks>
    [Fact]
    public async Task IncludeKeyAndExcludeKeyWithSpaceSeparatedMultipleValuesMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\nFOO=bar\nBAZ=qux\n"), null,
            "get", "--include-key", "HELLO", "FOO", "--exclude-key", "FOO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
