using Dotenvx.Core.Interop.EciesJs;

using Shouldly;

namespace Dotenvx.Test.Interop.EciesJs;

public class PublicKeyTest : EciesJsTestBase
{
    [Theory]
    // Calculated by eciesjs 0.4.15 Buffer.from(AlicePrivateKey.encapsulate(BobPublicKey, false)).toString("hex")
    [InlineData(
        AlicePrivateKeyHex,
        BobPublicKeyHex,
        "b92f936db3d6edc56fe6f71deb78e21674eefb8b46eb08f1d18ea9c93256909a")]
    // Calculated by eciesjs 0.4.15 Buffer.from(BobPrivateKey.encapsulate(AlicePublicKey, false)).toString("hex")
    [InlineData(
        BobPrivateKeyHex,
        AlicePublicKeyHex,
        "9208dc63cb2f85470216148e08a419daa4c60df3ae26c7a5fcd0aa9bd86eaf1a")]
    public void Ecdh_ComputesExpectedSharedSecret(string privateKeyHex, string peerPublicKeyHex, string expectedSharedHex)
    {
        byte[] privateKey = Convert.FromHexString(privateKeyHex);
        byte[] peerPublicKey = Convert.FromHexString(peerPublicKeyHex);
        byte[] expectedShared = Convert.FromHexString(expectedSharedHex);

        byte[] shared = new PublicKey(peerPublicKey).Decapsulate(privateKey);
        Assert.Equal(expectedShared, shared);
    }

    [Fact]
    public void Ecdh_ComputesSameSharedSecret()
    {
        // Calculated by eciesjs 0.4.15 Buffer.from(BobPrivateKey.encapsulate(AlicePublicKey, false)).toString("hex")
        byte[] aliceShared = PrivateKey.FromHex(AlicePrivateKeyHex).Encapsulate(Convert.FromHexString(BobPublicKeyHex));
        // Calculated by eciesjs 0.4.15 Buffer.from(BobPublicKey.decapsulate(AlicePrivateKey, false)).toString("hex")
        byte[] bobShared = PublicKey.FromHex(AlicePublicKeyHex).Decapsulate(Convert.FromHexString(BobPrivateKeyHex));
        aliceShared.ShouldBe(bobShared);
    }

    [Fact]
    public void Ecdh_Invalid_PublicKey_Throws()
    {
        byte[] badPublicKey = new byte[33];
        badPublicKey[0] = 0x01; // invalid prefix
        Should.Throw<ArgumentException>(() => new PublicKey(badPublicKey).Decapsulate(Convert.FromHexString(AlicePrivateKeyHex)));
    }

    [Fact]
    public void Ecdh_Invalid_PrivateKey_Throws()
    {
        byte[] badPrivateKey = new byte[10]; // too short
        Should.Throw<ArgumentException>(() => PublicKey.FromHex(BobPublicKeyHex).Decapsulate(badPrivateKey));
    }

    [Fact]
    public void Ecdh_Returns32Bytes()
    {
        byte[] secret = PublicKey.FromHex(BobPublicKeyHex).Decapsulate(Convert.FromHexString(AlicePrivateKeyHex));
        secret.Length.ShouldBe(32);
    }
}