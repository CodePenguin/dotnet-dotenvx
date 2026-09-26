using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Any scenario below that calls `set` with encryption enabled (the default - i.e. no `--plain`)
/// against a `.env` with no pre-existing `DOTENV_PUBLIC_KEY` hits fresh key GENERATION, which by
/// default routes Node's CLI through the native OS credential store (see
/// EncryptDecryptParityTest.cs's class remarks for the full explanation) - flaky/non-deterministic
/// on this machine, confirmed 2026-09-26 by several of these tests intermittently failing with
/// "failed to save private key to Windows Credential Manager" or a structurally-wrong .env (Node
/// silently skipped writing .env.keys because it went to the native store instead). Fixed the same
/// way as `EncryptDecryptParityTest`'s own fresh-key tests: pass `--no-native --no-armor` to
/// Node's call ONLY (this port's `set` has no such flags at all - passing them errors "Unrecognized
/// command or argument" and exits 1, confirmed live - so these can never be shared args in a
/// `RunBothAsync` call for these scenarios; each such scenario below calls `RunNodeAsync`/
/// `RunDotnetAsync` separately instead).
/// </remarks>
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

        void Setup()
        {
            WriteFile(".env", "HELLO=World\n");
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar", "--no-native", "--no-armor");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar");

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
        await Fixture.RunNodeAsync(WorkDir, null, "set", "FOO", "bar", "--no-native", "--no-armor");
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

        CliResult node = await Fixture.RunNodeAsync(WorkDir, null,
            "set", "FOO", "bar", "-f", "nonexistent.env", "--no-native", "--no-armor");
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FOO", "bar", "-f", "nonexistent.env");

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

    /// <remarks>
    /// Regression test for a 2026-09-26 bug: `set.js` registers `-c`/`--encrypt` as a bare
    /// commander boolean defaulting to true with no `--no-encrypt` negation - `--plain` is the
    /// ONLY thing that can ever turn encryption off, so it wins even when `-c` is ALSO explicitly
    /// passed. This port's `encrypt = encrypt || !plain` gave the explicit `-c` priority instead,
    /// silently encrypting when Node stores plain text.
    /// </remarks>
    [Fact]
    public async Task PlainFlagWinsOverExplicitEncryptFlagWhenBothGiven()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "set", "FOO", "bar", "--plain", "-c");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardError.ShouldBe(node.StandardError);
        ReadFile(".env").ShouldContain("FOO=\"bar\"");
    }
}
