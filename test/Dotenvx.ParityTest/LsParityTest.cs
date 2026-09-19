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
}
