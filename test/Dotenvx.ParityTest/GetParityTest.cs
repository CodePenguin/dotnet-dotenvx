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

    /// <remarks>
    /// Not from a JS test - found 2026-10-02 auditing `ls`: System.Text.Json's default encoder printed
    /// `&` as `\u0026` (and `<`, `>`, `+`, `'`, every non-ASCII character, DEL, U+2028 ...) where Node's
    /// `JSON.stringify` prints them literally, so a URL value with `&` in `get` output differed byte-for-byte.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetJsonEscapesLikeJsonStringify(bool prettyPrint)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string content = string.Concat(
            "URL='http://x?a=1&b=<2>+3\\'q'\n",
            "UNI='héllo😀\u2028\u00A0\uFEFF'\n",
            "CTRL='x", (char)1, (char)0x1f, (char)0x7f, "y'\n",
            "TEXT='a\\u00e9\\b'\n");

        string[] args = prettyPrint ? ["get", "--pretty-print"] : ["get"];
        (CliResult node, CliResult dotnet) = await RunBothAsync(() => WriteFile(".env", content), null, args);

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

    /// <remarks>
    /// Regression test found 2026-10-04 verifying multi-value `-fk`: `EnvItemOrdering`'s raw-token
    /// walk treated `-f`/`--env-file` as greedy and swallowed the positional KEY as one more env FILE
    /// (upstream `-f, --env-file &lt;path&gt;` takes exactly ONE value per occurrence, while the
    /// variadic `-e, --env &lt;strings...&gt;` really is greedy). So `dotenvx get -f .env HELLO` - the
    /// single most common `get` form - also reported a spurious
    /// `[MISSING_ENV_FILE] missing file (HELLO)` and exited 1 where Node prints `World` and exits 0.
    /// Each row pairs the one-value-per-occurrence `-f` against the genuinely variadic `-e`, so a
    /// regression that reverts either half fails here.
    /// </remarks>
    [Theory]
    [InlineData("-f .env HELLO")]
    [InlineData("--env-file .env HELLO")]
    [InlineData("-f .env MISSING_KEY")]
    [InlineData("-f .env -f .env.production HELLO")]
    [InlineData("-e FOO=bar HELLO")]
    [InlineData("-f .env -e FOO=bar HELLO")]
    public async Task GetEnvOptionArityDoesNotSwallowThePositionalKey(string argLine)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() =>
        {
            WriteFile(".env", "HELLO=World\n");
            WriteFile(".env.production", "HELLO=Production\n");
        }, null, ["get", .. argLine.Split(' ')]);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
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

    /// <remarks>
    /// Not from a JS test - found 2026-10-02 via a static audit cross-checking this port's
    /// Picomatch reimplementation against the real, live-callable `@dotenvx/primitives` `scan()`
    /// export it stands in for. `scan()`'s `ik`/`ek` wildcard matching never matches a leading dot
    /// by default (confirmed live), but this port's Picomatch always behaved as if `dot: true` -
    /// `get --include-key "*"` on a file with a dot-prefixed (but validly parseable) key name
    /// diverged: Node excluded it, this port included it. See IndexJs.Picomatch's own doc comment
    /// for the fix.
    /// </remarks>
    [Fact]
    public async Task IncludeKeyWildcardExcludesDotPrefixedKeyIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", ".SECRET=hidden\nFOO=bar\n"), null, "get", "--include-key", "*");

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

    /// <remarks>
    /// Regression test for a 2026-09-28 bug: JavaScript's `\w` regex metacharacter is ASCII-only
    /// (`[A-Za-z0-9_]`), but .NET's default `\w` is Unicode-aware and matches letters like "É" too -
    /// so a key like `HÉLLO=World` was silently accepted as a valid key by this port's parser
    /// while Node's real parser rejects the whole line (`{}` - not even a partial match), confirmed
    /// live. Fixed by adding `RegexOptions.ECMAScript` (which makes `\w` ASCII-only, matching JS
    /// exactly) to every regex in this port that uses `[\w.-]+` for key matching.
    /// </remarks>
    [Fact]
    public async Task NonAsciiKeyNameIsRejectedIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HÉLLO=World\nHELLO=World\n"), null,
            "get", "--format", "json");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldBe("{\"HELLO\":\"World\"}\n");
    }

    /// <remarks>Not from a JS test - an unknown option must be rejected (exit 1, nothing on stdout) rather than treated as the KEY positional.</remarks>
    [Fact]
    public async Task UnknownOptionIsRejectedOnBothSides()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "get", "--totally-made-up-flag", "HELLO");

        node.ExitCode.ShouldBe(1);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldContain("unknown option '--totally-made-up-flag'");
    }
}
