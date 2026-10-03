using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - drift checks for behavior of the encrypt/decrypt/set transforms that only showed up
/// once they were ported 1:1 from `transforms/*.js` and compared against the real CLI: the keys file defaults to
/// `.env.keys` in the WORKING directory (not next to the env file), the public key header notes it with
/// `# -fk <relative path>`, and `decrypt` upserts every parsed value (so double-quoted escapes are rewritten).
/// Node gets `--no-native --no-armor` where it would otherwise generate keys (see EncryptDecryptParityTest).
/// </remarks>
public class TransformParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [Fact]
    public async Task EncryptOfANestedEnvFileWritesKeysToTheWorkingDirectoryIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile("sub/.env", "A=1\n");
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "-f", "sub/.env", "--no-native",
            "--no-armor");
        string nodeEnv = RedactKeyMaterial(ReadFile("sub/.env"));
        string nodeKeys = RedactKeyMaterial(ReadFile(".env.keys"));
        File.Exists(Path.Combine(WorkDir, "sub", ".env.keys")).ShouldBeFalse();

        File.Delete(Path.Combine(WorkDir, ".env.keys"));
        WriteFile("sub/.env", "A=1\n");
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "encrypt", "-f", "sub/.env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        RedactKeyMaterial(dotnet.StandardOutput).ShouldBe(RedactKeyMaterial(node.StandardOutput));
        File.Exists(Path.Combine(WorkDir, "sub", ".env.keys")).ShouldBeFalse();
        RedactKeyMaterial(ReadFile("sub/.env")).ShouldBe(nodeEnv);
        RedactKeyMaterial(ReadFile(".env.keys")).ShouldBe(nodeKeys);
    }

    [Fact]
    public async Task DecryptRewritesEveryParsedValueIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "A=\"line1\\nline2\"\nB='raw\\nstay'\nC=plain\n"), null, "decrypt", "--stdout");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task DecryptOfANestedEnvFileLooksForKeysInTheWorkingDirectoryOnly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() =>
        {
            WriteFile("sub/.env", FixedKeypairFixture.EncryptedEnv);
            WriteFile("sub/.env.keys", FixedKeypairFixture.PrivateKeyFile);
        }, null, "decrypt", "--stdout", "-f", "sub/.env");

        node.ExitCode.ShouldBe(1);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
    }

    [Fact]
    public async Task DecryptOfANestedEnvFileWithKeysFileFlagMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() =>
        {
            WriteFile("sub/.env", FixedKeypairFixture.EncryptedEnv);
            WriteFile("sub/.env.keys", FixedKeypairFixture.PrivateKeyFile);
        }, null, "decrypt", "--stdout", "-f", "sub/.env", "-fk", "sub/.env.keys");

        node.ExitCode.ShouldBe(0);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
