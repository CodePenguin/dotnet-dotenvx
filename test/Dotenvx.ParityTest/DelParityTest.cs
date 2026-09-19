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
}
