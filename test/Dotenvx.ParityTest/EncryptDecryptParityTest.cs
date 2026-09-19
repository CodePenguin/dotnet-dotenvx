using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Most tests here deliberately do NOT test "fresh key generation from a manually-provided
/// .env.keys" cross-implementation, even though that's what an earlier version of this suite
/// tried: at our pinned commit, encrypt's "no existing public key yet" path by default routes
/// through the newer keyring/native-store resolver (src/lib/resolvers/keypair.js), which does not
/// reliably use a plain .env.keys private key for fresh generation and is not deterministic
/// run-to-run - exactly the custodian/keyring reorg territory this port has deliberately deferred
/// (see .claude/plan.md). Real cross-compatibility (the actual goal) is proven instead with a
/// fixed, pre-verified ciphertext generated once via our own .NET encrypt - if both CLIs can
/// independently decrypt the exact same bytes, the ECIES/secp256k1 implementations interoperate,
/// without depending on either side's fresh-key resolution behavior.
///
/// The two "GeneratesFreshKeypairThat...Uses" tests below are the exception: they test actual
/// fresh key GENERATION cross-implementation, made deterministic-enough by forcing Node's encrypt
/// off the keyring/native-store path with `--no-native --no-armor`, which makes it fall back to
/// plain-file `.env.keys` writing (confirmed by inspecting the file's existence, not just
/// behavior). Without those flags, Node's fresh encrypt does NOT write `.env.keys` at all (verified
/// empirically), so .NET has no private key to decrypt with - that's the specific failure this
/// forces around. .NET's encrypt has no native/armor concept (out of scope for this port) so it
/// always plain-file-generates already and needs no equivalent flag.
/// </remarks>
public class EncryptDecryptParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private void WriteFixedEncryptedFixture()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
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

    /// <remarks>
    /// Genuine fresh key GENERATION (not reuse) cross-implementation: .NET encrypts an empty .env
    /// with no pre-existing .env.keys, generating a brand-new keypair and writing it to .env.keys,
    /// then Node decrypts using that freshly-written file. The actual key bytes differ every run
    /// (that's expected and fine) - what's being proven is that whatever .NET generates, Node can
    /// immediately use.
    /// </remarks>
    [Fact]
    public async Task DotnetGeneratesFreshKeypairThatNodeUses()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\n");
        CliResult encrypt = await Fixture.RunDotnetAsync(WorkDir, null, "encrypt");
        encrypt.ExitCode.ShouldBe(0);
        File.Exists(Path.Combine(WorkDir, ".env.keys")).ShouldBeTrue();

        CliResult get = await Fixture.RunNodeAsync(WorkDir, null, "get", "HELLO");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("World");
    }

    /// <remarks>
    /// The reverse direction of <see cref="DotnetGeneratesFreshKeypairThatNodeUses"/>: Node
    /// encrypts an empty .env with no pre-existing .env.keys, using `--no-native --no-armor` to
    /// force plain-file key generation (see class remarks) instead of routing through the
    /// keyring/native-store resolver, then .NET decrypts using that freshly-written file.
    /// </remarks>
    [Fact]
    public async Task NodeGeneratesFreshKeypairThatDotnetUses()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        WriteFile(".env", "HELLO=World\n");
        CliResult encrypt = await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "--no-native", "--no-armor");
        encrypt.ExitCode.ShouldBe(0);
        File.Exists(Path.Combine(WorkDir, ".env.keys")).ShouldBeTrue();

        CliResult get = await Fixture.RunDotnetAsync(WorkDir, null, "get", "HELLO");

        get.ExitCode.ShouldBe(0);
        get.StandardOutput.Trim().ShouldBe("World");
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
            WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
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
