using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// keypair's resolver (src/lib/resolvers/keypair.js) looks up the private key by matching an
/// ALREADY-PRESENT DOTENV_PUBLIC_KEY in the .env file against a keyring - it does not derive a
/// public key from an arbitrary private key dropped in .env.keys with no matching public key yet.
/// So the fixture here must be an already-encrypted file, not a bare private key.
/// </remarks>
public class KeypairParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private void WriteFixedEncryptedFixture()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
    }

    [Fact]
    public async Task KeypairOutputMatchesExactlyForAKnownKeyPair()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        // Same key pair on both sides (not freshly generated), so this is an exact match, no
        // redaction needed.
        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteFixedEncryptedFixture, null, "keypair");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    [Fact]
    public async Task KeypairSingleKeyLookupMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(WriteFixedEncryptedFixture, null, "keypair", "DOTENV_PUBLIC_KEY");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Same JsonSerializerOptions.WriteIndented/Environment.NewLine bug as GetParityTest's
    /// PrettyPrintFormatMatchesExactly - fixed in KeypairAction.cs.
    /// </remarks>
    [Fact]
    public async Task KeypairPrettyPrintMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(WriteFixedEncryptedFixture, null, "keypair", "--pretty-print");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }
}
