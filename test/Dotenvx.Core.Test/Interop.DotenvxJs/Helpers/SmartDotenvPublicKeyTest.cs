using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No longer a distinct current upstream scenario - historical coverage from a pre-deletion
/// smart-dotenv-public-key.test.js, which no longer exists anywhere in the submodule at this pin
/// (confirmed via find - the file this port ported it from, smartDotenvPublicKey.js, was itself
/// deleted upstream well before v2.24.1, per SmartDotenvPublicKey.cs's own doc comment). The two
/// process-env-related tests below were rewritten 2026-10-02 to assert the corrected, live-verified
/// current behavior (process.env is never consulted for the PUBLIC key - only the private key still
/// has a process.env fallback, via SmartDotenvPrivateKey.cs) after a code audit found and fixed a
/// real bug: this port previously checked process.env FIRST, so a stray DOTENV_PUBLIC_KEY left in
/// the shell environment could make `encrypt`/`set` on a brand-new .env file crash or silently
/// encrypt against the wrong key.
/// </remarks>
public class SmartDotenvPublicKeyTest : EnvironmentTestBase
{
    private const string FilePath = ".env";

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyReturnsNull()
    {
        string? result = SmartDotenvPublicKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>
    /// Not from a JS test - regression test for the 2026-10-02 fix: a DOTENV_PUBLIC_KEY set in the
    /// process environment must be ignored entirely when the target .env file itself has no public
    /// key, matching real upstream's file-only `publickeys(src)[0]` lookup.
    /// </remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyIgnoresProcessEnvWhenFileHasNoPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "<publicKey>");

        string? result = SmartDotenvPublicKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>
    /// Not from a JS test - companion to the above, confirming a process-env DOTENV_PUBLIC_KEY is
    /// ignored even when the target file DOES have its own (different) public key - the file's own
    /// value must win, not the env var.
    /// </remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyIgnoresProcessEnvWhenFileHasItsOwnPublicKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "<publicKey>");

        string? result = SmartDotenvPublicKey(filepath);

        result.ShouldBe("03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");
    }

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyWhenEnvKeysPresentReturnsPublicKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";

        string? result = SmartDotenvPublicKey(filepath);

        result.ShouldBe("03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");
    }
}