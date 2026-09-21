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

    /// <remarks>
    /// Task D - default (no --no-create) on a missing env file: both CLIs CREATE the .env with
    /// the plain pair. Previously --plain set against a missing file diverged (Node created it,
    /// ours errored MISSING_ENV_FILE and never ran the Sets service path that would have created
    /// it) - see SetAction.cs/Task D and Replace.cs empty-src fix.
    /// </remarks>
    [Fact]
    public async Task SetOnMissingEnvFileCreatesItByDefault()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            if (File.Exists(Path.Combine(WorkDir, ".env")))
            {
                File.Delete(Path.Combine(WorkDir, ".env"));
            }
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar", "--plain");
        string nodeEnvFile = ReadFile(".env");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar", "--plain");
        string dotnetEnvFile = ReadFile(".env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnetEnvFile.ShouldBe(nodeEnvFile);
        dotnetEnvFile.ShouldContain("FOO=\"bar\"\n");
    }

    /// <remarks>Task D - --no-create opt-in: both CLIs exit 1 and write nothing, matching the
    /// old (now opt-in) MISSING_ENV_FILE behavior.</remarks>
    [Fact]
    public async Task SetOnMissingEnvFileWithNoCreateFlagErrorsIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            if (File.Exists(Path.Combine(WorkDir, ".env")))
            {
                File.Delete(Path.Combine(WorkDir, ".env"));
            }
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar", "--no-create");
        string nodeEnvFile = File.Exists(Path.Combine(WorkDir, ".env")) ? ReadFile(".env") : "";

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar", "--no-create");
        string dotnetEnvFile = File.Exists(Path.Combine(WorkDir, ".env")) ? ReadFile(".env") : "";

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldNotBe(0);
        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }
}
