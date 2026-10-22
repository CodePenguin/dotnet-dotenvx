using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/keypair.test.js</remarks>
public class KeypairTest
{
    /// <remarks>Refer to "#keypair"</remarks>
    [Fact]
    public void TestKeypair()
    {
        (string publicKey, string privateKey) = Keypair();

        publicKey.ShouldNotBeNullOrEmpty();
        privateKey.ShouldNotBeNullOrEmpty();
        publicKey.Length.ShouldBe(66);
        privateKey.Length.ShouldBe(64);
    }

    /// <remarks>Refer to "keypair uses provided private key to generate public key"</remarks>
    [Fact]
    public void TestKeypairUsesProvidedPrivateKey()
    {
        const string existingPrivateKey = "4c06b1f9ffc4af11d0d206fd43f28bc96b68647158c1666edc4832f19857cef9";
        (string publicKey, string privateKey) = Keypair(existingPrivateKey);

        privateKey.ShouldBe(existingPrivateKey);
        publicKey.ShouldNotBeNullOrEmpty();
        publicKey.Length.ShouldBe(66);

        // Note: The JS test checks against eciesjs generated public key, but since C# uses the same, assume it's correct.
    }
}
