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
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";

    private const string EncryptedEnv =
        "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/\n" +
        "#/            public-key encryption for .env files          /\n" +
        "#/       [how it works](https://dotenvx.com/encryption)     /\n" +
        "#/----------------------------------------------------------/\n" +
        $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n\n" +
        "# .env\n" +
        "HELLO=encrypted:BGftYDEUfycZeomeqB4m8HxtfNH3Pe8TxCNAodAJ2Q7t1x8J4iFFY0TjDu1MNIzJKl3DLDT33oDTCVWOBZe1ZvC/RWZy5F92dj6qPPvhA9e7kuifQwrinrEirS9XJINSqHOyyeIP\n";

    private void WriteFixedEncryptedFixture()
    {
        WriteFile(".env", EncryptedEnv);
        WriteFile(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");
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
}
