using Shouldly;

namespace Dotenvx.ParityTest;

public class DelParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task DeletingAnExistingKeyMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            WriteFile(".env", "HELLO=World\nFOO=bar\nBAZ=qux\n");
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "del", "FOO");
        string nodeEnvFile = ReadFile(".env");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "del", "FOO");
        string dotnetEnvFile = ReadFile(".env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }

    [Fact]
    public async Task DeletingAMissingKeyReportsNoChangeIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "del", "NOTFOUND");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task DeletingFromAMissingFileMatchesExitCode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(() => { }, null, "del", "FOO", "-f", "nonexistent.env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
    }

    /// <remarks>
    /// Cross-implementation coverage: one CLI deletes a key, the other CLI independently confirms
    /// both the removal (missing-key exit code) and that the untouched key is still readable -
    /// proving the two CLIs agree on the resulting file, not just on their own view of it.
    /// </remarks>
    [Fact]
    public async Task NodeDeletesKeyThatDotnetConfirmsRemoved()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\nFOO=bar\n");
        CliResult del = await Fixture.RunNodeAsync(WorkDir, null, "del", "FOO");
        del.ExitCode.ShouldBe(0);

        CliResult getDeleted = await Fixture.RunDotnetAsync(WorkDir, null, "get", "FOO");
        getDeleted.ExitCode.ShouldBe(1);

        CliResult getRemaining = await Fixture.RunDotnetAsync(WorkDir, null, "get", "HELLO");
        getRemaining.ExitCode.ShouldBe(0);
        getRemaining.StandardOutput.Trim().ShouldBe("World");
    }

    [Fact]
    public async Task DotnetDeletesKeyThatNodeConfirmsRemoved()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\nFOO=bar\n");
        CliResult del = await Fixture.RunDotnetAsync(WorkDir, null, "del", "FOO");
        del.ExitCode.ShouldBe(0);

        CliResult getDeleted = await Fixture.RunNodeAsync(WorkDir, null, "get", "FOO");
        getDeleted.ExitCode.ShouldBe(1);

        CliResult getRemaining = await Fixture.RunNodeAsync(WorkDir, null, "get", "HELLO");
        getRemaining.ExitCode.ShouldBe(0);
        getRemaining.StandardOutput.Trim().ShouldBe("World");
    }
}
