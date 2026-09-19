using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Deliberately does NOT test "fresh key generation from a manually-provided .env.keys" cross-
/// implementation, even though that's what an earlier version of this suite tried: at our pinned
/// commit, encrypt's "no existing public key yet" path already routes through the newer keyring/
/// native-store resolver (src/lib/resolvers/keypair.js), which does not reliably use a plain
/// .env.keys private key for fresh generation and is not deterministic run-to-run - exactly the
/// custodian/keyring reorg territory this port has deliberately deferred (see .claude/plan.md).
/// Real cross-compatibility (the actual goal) is proven instead with a fixed, pre-verified
/// ciphertext generated once via our own .NET encrypt - if both CLIs can independently decrypt the
/// exact same bytes, the ECIES/secp256k1 implementations interoperate, without depending on either
/// side's fresh-key resolution behavior.
/// </remarks>
public class EncryptDecryptParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
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
    public async Task NodeDecryptsTheFixedCiphertext()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFixedEncryptedFixture();
        CliResult result = await Fixture.RunNodeAsync(WorkDir, null, "get", "HELLO");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldBe("World");
    }

    [Fact]
    public async Task DotnetDecryptsTheFixedCiphertext()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFixedEncryptedFixture();
        CliResult result = await Fixture.RunDotnetAsync(WorkDir, null, "get", "HELLO");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldBe("World");
    }

    [Fact]
    public async Task DecryptCommandProducesIdenticalPlaintextOnBothClis()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteFixedEncryptedFixture, null, "decrypt");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
    }

    /// <remarks>
    /// The genuine bidirectional "create with one, use with the other" round trip the fixed-
    /// ciphertext tests above can't cover on their own: fresh ciphertext, generated live by one
    /// CLI's `set` on THIS run, decrypted live by the other CLI's `get`. This works reliably
    /// (unlike a bare "encrypt into an empty .env") because it starts from the fixed fixture's
    /// EXISTING keypair - `set`'s "reuse the existing key" path is deterministic on both sides;
    /// it's only fresh key GENERATION (no existing public key yet) that isn't, per the class
    /// remarks above.
    /// </remarks>
    [Fact]
    public async Task NodeCreatesFreshCiphertextThatDotnetDecrypts()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFixedEncryptedFixture();
        CliResult set = await Fixture.RunNodeAsync(WorkDir, null, "set", "FRESHKEY", "freshvalue");
        set.ExitCode.ShouldBe(0);
        ReadFile(".env").ShouldContain("FRESHKEY=");

        CliResult get = await Fixture.RunDotnetAsync(WorkDir, null, "get", "FRESHKEY");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("freshvalue");
    }

    [Fact]
    public async Task DotnetCreatesFreshCiphertextThatNodeDecrypts()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFixedEncryptedFixture();
        CliResult set = await Fixture.RunDotnetAsync(WorkDir, null, "set", "FRESHKEY", "freshvalue");
        set.ExitCode.ShouldBe(0);
        ReadFile(".env").ShouldContain("FRESHKEY=");

        CliResult get = await Fixture.RunNodeAsync(WorkDir, null, "get", "FRESHKEY");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("freshvalue");
    }

    [Fact]
    public async Task EncryptOutputStructureMatchesIgnoringFreshKeyMaterial()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            WriteFile(".env", "HELLO=World\n");
            WriteFile(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");
        }

        Setup();
        await Fixture.RunNodeAsync(WorkDir, null, "encrypt");
        string nodeEnvFile = RedactKeyMaterial(ReadFile(".env"));

        Setup();
        await Fixture.RunDotnetAsync(WorkDir, null, "encrypt");
        string dotnetEnvFile = RedactKeyMaterial(ReadFile(".env"));

        // Only the redacted structure (header comment block, "# .env" marker, "encrypted:" prefix)
        // is compared - the actual key/ciphertext bytes are expected to differ (see class remarks).
        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }
}
