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

    /// <remarks>--format colon was entirely unimplemented (a real gap, only shell/json worked).</remarks>
    [Fact]
    public async Task KeypairColonFormatMatchesExactly()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(WriteFixedEncryptedFixture, null, "keypair", "--format", "colon");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>--pp is a separate alias for --pretty-print upstream; was entirely missing.</remarks>
    [Fact]
    public async Task KeypairPpAliasMatchesPrettyPrint()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) =
            await RunBothAsync(WriteFixedEncryptedFixture, null, "keypair", "--pp");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
    }

    /// <remarks>
    /// Regression test for a 2026-09-29 bug found via a code audit: `keypair.js`'s single-KEY
    /// branch checks `results === undefined` (key entirely absent), not falsy - a key that WAS
    /// derived but has a `null` value (e.g. `DOTENV_PUBLIC_KEY` on a plaintext-only file) still
    /// falls through to `console.log(results)`, printing the literal text "null", exit 0.
    /// `KeyPairAction.cs` used `Dictionary.GetValueOrDefault`, which can't distinguish "absent" from
    /// "present but null" - it took the "not found" branch (empty line, exit 1) instead.
    /// </remarks>
    [Fact]
    public async Task KeyPairSingleKeyWithNullValuePrintsLiteralNullNotEmpty()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(
            () => WriteFile(".env", "HELLO=World\n"), null, "keypair", "DOTENV_PUBLIC_KEY");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.ExitCode.ShouldBe(0);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.Trim().ShouldBe("null");
    }
}
