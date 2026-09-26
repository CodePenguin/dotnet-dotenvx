using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// This suite found real bugs the first time `ls` was exercised end-to-end: the default
/// ".env*" pattern was never applied (an empty pattern list silently matched nothing), and the
/// applied patterns weren't "**/"-prefixed so nested files were invisible even once the default
/// was fixed. Fixed in Ls.cs - see .claude/plan.md. The "found N .env files..." summary line
/// upstream prints is on stderr and includes elapsed time, so per this suite's stderr-wording
/// convention (CLAUDE.md trap #4) it's not compared here - only the deterministic tree/JSON on
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
    /// CLAUDE.md's trap for the full list this affected across the whole CLI, not just `ls`).
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
}
