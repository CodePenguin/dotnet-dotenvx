using Shouldly;

namespace Dotenvx.ParityTest;

public class SetParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task SetPlainValueMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            WriteFile(".env", "HELLO=World\n");
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar", "--plain");
        string nodeEnvFile = ReadFile(".env");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar", "--plain");
        string dotnetEnvFile = ReadFile(".env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }

    [Fact]
    public async Task SetEncryptedValueMatchesStructureIgnoringFreshKeyMaterial()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "set", "FOO", "bar");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        RedactKeyMaterial(dotnet.StandardOutput).ShouldBe(RedactKeyMaterial(node.StandardOutput));
    }

    [Fact]
    public async Task SetEncryptedEnvFileStructureMatchesIgnoringFreshKeyMaterial()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            WriteFile(".env", "HELLO=World\n");
        }

        Setup();
        await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar");
        string nodeEnvFile = RedactKeyMaterial(ReadFile(".env"));

        Setup();
        await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar");
        string dotnetEnvFile = RedactKeyMaterial(ReadFile(".env"));

        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }

    [Fact]
    public async Task SetOnMissingEnvFileMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(() => { }, null, "set", "FOO", "bar", "-f", "nonexistent.env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
    }
}
