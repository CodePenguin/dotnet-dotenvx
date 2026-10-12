using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Most tests here deliberately do NOT test "fresh key generation from a manually-provided
/// .env.keys" cross-implementation, even though that's what an earlier version of this suite
/// tried: at our pinned commit, encrypt's "no existing public key yet" path by default routes
/// through the newer keyring/native-store resolver (src/lib/resolvers/keypair.js), which does not
/// reliably use a plain .env.keys private key for fresh generation and is not deterministic
/// run-to-run - exactly the custodian/keyring reorg territory this port has deliberately deferred
/// (see docs/porting-traps.md). Real cross-compatibility (the actual goal) is proven instead with a
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
///
/// Fixed 2026-09-26: several OTHER tests below - not just the two "fresh key" ones - ALSO
/// incidentally trigger this exact same fresh-key/native-store path as a side effect (encrypting a
/// `.env` with no pre-existing `DOTENV_PUBLIC_KEY`, regardless of whether `.env.keys` already has a
/// private key sitting in it - `resolvers/keypair.js` looks for the public key in the `.env` file's
/// own content first, so an unrelated `.env.keys` file doesn't help), even though none of them are
/// actually testing native/armor behavior - confirmed by intermittent "failed to save private key
/// to Windows Credential Manager" failures and structurally-wrong `.env` files (Node silently
/// skipping the `.env.keys` write because it went to the native store instead). Added
/// `--no-native --no-armor` to each affected test's Node call - never to `RunBothAsync`'s shared
/// args, since this port's `set`/`encrypt` have no such flags at all and error out immediately if
/// given them (confirmed live: "Unrecognized command or argument", exit 1) - each such scenario
/// calls `RunNodeAsync`/`RunDotnetAsync` separately instead.
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
    /// Found a real bug here: DecryptAction's main branch tracked errorCount per-file but never
    /// checked it before returning - always returned 0 regardless of errors, unlike its own
    /// --stdout branch (which already had the check) and encrypt's equivalent bug fixed earlier.
    /// </remarks>
    [Fact]
    public async Task DecryptOnMissingEnvFileExitsNonZeroIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "decrypt");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldNotBe(0);
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
        await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "--no-native", "--no-armor");
        string nodeEnvFile = RedactKeyMaterial(ReadFile(".env"));

        Setup();
        await Fixture.RunDotnetAsync(WorkDir, null, "encrypt");
        string dotnetEnvFile = RedactKeyMaterial(ReadFile(".env"));

        // Only the redacted structure (header comment block, "# .env" marker, "encrypted:" prefix)
        // is compared - the actual key/ciphertext bytes are expected to differ (see class remarks).
        dotnetEnvFile.ShouldBe(nodeEnvFile);
    }

    /// <remarks>
    /// Implements encrypt's default "create a sample .env when missing" behavior, previously
    /// entirely unimplemented (a real gap: .NET used to always error MISSING_ENV_FILE here, even
    /// though that's not what Node actually does by default - see Encrypt.cs's SampleEnvKit).
    /// Redacted structural comparison, not byte-for-byte, since fresh key material differs.
    /// </remarks>
    [Fact]
    public async Task EncryptOnMissingEnvFileCreatesSampleAndEncrypts()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "--no-native", "--no-armor");
        node.ExitCode.ShouldBe(0);
        string nodeEnvFile = RedactKeyMaterial(ReadFile(".env"));
        File.Delete(Path.Combine(WorkDir, ".env"));
        File.Delete(Path.Combine(WorkDir, ".env.keys"));

        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "encrypt");
        dotnet.ExitCode.ShouldBe(0);
        string dotnetEnvFile = RedactKeyMaterial(ReadFile(".env"));

        dotnetEnvFile.ShouldBe(nodeEnvFile);
        dotnetEnvFile.ShouldContain("encrypted:REDACTED");
    }

    /// <remarks>Reverse of <see cref="EncryptOnMissingEnvFileCreatesSampleAndEncrypts"/>: --no-create disables that default, matching the old (still correct, just now opt-in) MISSING_ENV_FILE error behavior.</remarks>
    [Fact]
    public async Task EncryptOnMissingEnvFileWithNoCreateFlagErrorsIdentically()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "encrypt", "--no-create");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldNotBe(0);
        File.Exists(Path.Combine(WorkDir, ".env")).ShouldBeFalse();
    }

    /// <remarks>
    /// -k/--key already worked (Encrypt.cs's _include), just lacked parity coverage - confirms only
    /// the named key(s) get encrypted while the rest stay plaintext, identically on both sides.
    /// </remarks>
    [Fact]
    public async Task KeyOptionEncryptsOnlyTheNamedKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        void Setup()
        {
            WriteFile(".env", "HELLO=World\nFOO=bar\n");
            WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
        }

        Setup();
        await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "--key", "HELLO", "--no-native", "--no-armor");
        string nodeEnvFile = RedactKeyMaterial(ReadFile(".env"));

        Setup();
        await Fixture.RunDotnetAsync(WorkDir, null, "encrypt", "--key", "HELLO");
        string dotnetEnvFile = RedactKeyMaterial(ReadFile(".env"));

        dotnetEnvFile.ShouldBe(nodeEnvFile);
        dotnetEnvFile.ShouldContain("encrypted:REDACTED");
        dotnetEnvFile.ShouldContain("FOO=bar");
    }

    /// <remarks>
    /// --stdout already worked, just lacked parity coverage. Also locks in a real bug found and
    /// fixed alongside --no-create: EncryptAction's --stdout branch never checked processedEnv.Error
    /// and always returned exit 0, unlike upstream which logs the error and exits 1 - see
    /// EncryptAction.cs.
    /// </remarks>
    [Fact]
    public async Task StdoutFlagPrintsWithoutWritingTheFile()
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
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "encrypt", "--stdout", "--no-native", "--no-armor");
        string nodeOutput = RedactKeyMaterial(node.StandardOutput);
        string envFileAfterNode = ReadFile(".env");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "encrypt", "--stdout");
        string dotnetOutput = RedactKeyMaterial(dotnet.StandardOutput);
        string envFileAfterDotnet = ReadFile(".env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnetOutput.ShouldBe(nodeOutput);
        envFileAfterNode.ShouldBe("HELLO=World\n");
        envFileAfterDotnet.ShouldBe("HELLO=World\n");
    }

    /// <remarks>Task E - decrypt --stdout --mask (bare, showChar default 6): identical masked
    /// stdout on both CLIs - values are masked (HELLO=World -> HELLO=******) identically.</remarks>
    [Fact]
    public async Task DecryptStdoutWithBareMaskMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteFixedEncryptedFixture, null, "decrypt", "--stdout", "--mask");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldContain("*");
        dotnet.StandardOutput.ShouldNotContain("HELLO=World");
    }

    /// <remarks>Task E - decrypt --stdout --mask 2 (custom showChar): identical masked stdout.
    /// showChar=2 -> HELLO=World shows first 2 visible chars then * to the same length.</remarks>
    [Fact]
    public async Task DecryptStdoutWithCustomMaskCharsMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteFixedEncryptedFixture, null, "decrypt", "--stdout", "--mask", "2");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>Task E regression guard - decrypt --stdout WITHOUT --mask is unaffected: masked
    /// output must never leak into plain --stdout output on either CLI.</remarks>
    [Fact]
    public async Task DecryptStdoutWithoutMaskIsUnaffected()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteFixedEncryptedFixture, null, "decrypt", "--stdout");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldNotContain("*");
        node.StandardOutput.ShouldNotContain("*");
    }

    /// <remarks>
    /// Regression test for a 2026-09-29 bug found via a code audit: `DecryptAction.cs`'s
    /// non-`--stdout` (file-write) branch passed `null` instead of the parsed `-k`/`--key` values
    /// to `Decrypt`'s constructor - a straight parameter-order/wiring mistake, not a different-but-
    /// equivalent code path. `dotenvx decrypt -k SPECIFIC_KEY` (writing to file) decrypted and
    /// rewrote ALL keys instead of only the one requested. The `--stdout` branch already correctly
    /// passed the filter through, which is why this port's own ParityTest coverage never caught it
    /// - every existing `-k` scenario happened to use `--stdout`.
    /// </remarks>
    [Fact]
    public async Task DecryptSpecificKeyOnFileWritePathOnlyDecryptsThatKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        const string publicKey = FixedKeypairFixture.PublicKey;
        const string encryptedTwoKeyEnv =
            $"DOTENV_PUBLIC_KEY=\"{publicKey}\"\n" +
            "HELLO=encrypted:BGftYDEUfycZeomeqB4m8HxtfNH3Pe8TxCNAodAJ2Q7t1x8J4iFFY0TjDu1MNIzJKl3DLDT33oDTCVWOBZe1ZvC/RWZy5F92dj6qPPvhA9e7kuifQwrinrEirS9XJINSqHOyyeIP\n" +
            "FOO=encrypted:BJdAk7BCmd3nQw2xoXRxxxdi1ncLnkJm0rSrQI6HLo5y2144H4HthRLgQFYQghf2SfVSaPbhDEgXlN2TeMqYfUnjkQOgqnS+T+MjI5V0a9eF6/h6ToKJ9gZEZEcx+PxDCctY6g==\n";

        void Setup()
        {
            WriteFile(".env", encryptedTwoKeyEnv);
            WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, null, "decrypt", "-k", "HELLO");
        string nodeEnvFile = ReadFile(".env");

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, null, "decrypt", "-k", "HELLO");
        string dotnetEnvFile = ReadFile(".env");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnetEnvFile.ShouldBe(nodeEnvFile);
        dotnetEnvFile.ShouldContain("HELLO=World");
        dotnetEnvFile.ShouldContain("FOO=encrypted:");
    }
}