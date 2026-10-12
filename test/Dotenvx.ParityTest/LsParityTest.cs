using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// This suite found real bugs the first time `ls` was exercised end-to-end: the default
/// ".env*" pattern was never applied (an empty pattern list silently matched nothing), and the
/// applied patterns weren't "**/"-prefixed so nested files were invisible even once the default
/// was fixed. Fixed in Ls.cs - see docs/porting-traps.md. The "found N .env files..." summary line
/// upstream prints is on stderr and includes elapsed time, so per this suite's stderr-wording
/// convention (docs/porting-traps.md trap #4) it's not compared here - only the deterministic tree/JSON on
/// stdout is.
/// </remarks>
public class LsParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private void WriteNestedFixture()
    {
        WriteFile(".env", "HELLO=World\n");
        WriteFile("a/.env", "FOO=bar\n");
        WriteFile("a/b/.env", "BAZ=qux\n");
    }

    [Fact]
    public async Task TreeOutputMatchesForNestedEnvFiles()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteNestedFixture, null, "ls");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task JsonOutputMatchesForNestedEnvFiles()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteNestedFixture, null, "ls", "--json");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task ExcludeEnvFilePatternMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            WriteNestedFixture, null, "ls", "-ef", "a/**");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task EmptyDirectoryProducesIdenticalOutput()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "ls");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Regression test for a 2026-09-26 crash: `-f`/`--env-file` is genuinely variadic upstream
    /// (`-f, --env-file &lt;filenames...&gt;`), but `System.CommandLine`'s `Option&lt;T[]?&gt;`
    /// defaults `AllowMultipleArgumentsPerToken` to false, so `ls -f a b` (space-separated, one
    /// flag occurrence) only captured "a" into the array and let "b" fall through to `ls`'s own
    /// `[directory]` positional argument - `Ls`'s constructor then treated the FILE "b" as a
    /// directory to crawl, producing an unhandled `IOException` ("The parameter is incorrect")
    /// instead of listing both files. Fixed by setting `AllowMultipleArgumentsPerToken = true` on
    /// every option whose upstream commander registration is genuinely `&lt;name...&gt;` (see
    /// docs/porting-traps.md, trap for the full list this affected across the whole CLI, not just `ls`).
    /// </remarks>
    [Fact]
    public async Task MultipleSpaceSeparatedEnvFilesInOneFlagOccurrenceMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () =>
            {
                WriteFile(".env.a", "A=1\n");
                WriteFile(".env.b", "B=2\n");
            }, null, "ls", "-f", ".env.a", ".env.b");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Regression test for a 2026-09-29 bug found via a code audit: `ls.js`'s Fdir crawler never
    /// descends into `node_modules`/`.git`, or a directory whose extension matches one of five
    /// macOS bundle formats (`.app`/`.key`/`.numbers`/`.pages`/`.photoslibrary`) -
    /// `DEFAULT_EXCLUDED_DIRECTORY_EXTENSIONS` in `resolvers/ls.js`. This port's `Ls.FilePaths()`
    /// enumerated the whole tree unconditionally, filtering afterward via glob patterns that had no
    /// equivalent for the bundle extensions - a `.env*` file inside e.g. a `.photoslibrary`
    /// directory was listed here but never even seen by Node's crawler.
    /// </remarks>
    [Fact]
    public async Task EnvFileInsideBundleExtensionDirectoryIsExcluded()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () =>
            {
                WriteFile(".env", "A=1\n");
                WriteFile("photos.photoslibrary/.env", "A=1\n");
            }, null, "ls");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>Not from a JS test - found by porting "ls does not print a summary in quiet mode": -q must suppress the stderr summary on both sides.</remarks>
    [Fact]
    public async Task QuietSuppressesTheSummaryLineOnBothSides()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteNestedFixture, null, "ls", "-q");

        node.StandardError.ShouldBeEmpty();
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
