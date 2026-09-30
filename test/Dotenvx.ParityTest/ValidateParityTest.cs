using Shouldly;

namespace Dotenvx.ParityTest;

public class ValidateParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private void WriteRequiredKeyFixture()
    {
        WriteFile(".env", "KEY=\n");
        WriteFile(".env.example", "KEY=\n");
    }

    /// <remarks>
    /// Regression test for a 2026-09-30 bug found via a code audit: `-e`/`-f` occurrences were
    /// grouped by type (every `-e` before every `-f`) instead of preserved in actual command-line
    /// order. Real Node pushes both into one shared array in argv order, and with `--overload` a
    /// LATER item wins for a duplicate key - so whichever of `-e`/`-f` comes SECOND on the command
    /// line determines the final value of a key both define. `.env` has an empty (missing-required)
    /// `KEY`; `-e "KEY=x"` supplies a non-empty value. With `-f .env` first and `-e` second, the
    /// `-e` value should win and validation should pass on both CLIs identically.
    /// </remarks>
    [Fact]
    public async Task EnvFlagAfterEnvFileWinsWithOverload()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            WriteRequiredKeyFixture, null, "validate", "-f", ".env", "-e", "KEY=fromenv", "--overload");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Companion to the above with the flags reversed - `-f` now comes SECOND, so its empty value
    /// should win and validation should fail identically on both CLIs. Confirms the fix reads actual
    /// argv order rather than just always preferring one flag type over the other.
    /// </remarks>
    [Fact]
    public async Task EnvFileFlagAfterEnvWinsWithOverload()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            WriteRequiredKeyFixture, null, "validate", "-e", "KEY=fromenv", "-f", ".env", "--overload");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(1);
    }
}
